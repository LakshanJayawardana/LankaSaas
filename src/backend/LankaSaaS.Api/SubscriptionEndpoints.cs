using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class SubscriptionEndpoints
{
    sealed record Plan(string Code,string Name,decimal Price,int UserLimit,string Description);
    static readonly Plan[] Plans=
    [
        new(SubscriptionPlans.Starter,"Starter",2_500,5,"For small teams getting started."),
        new(SubscriptionPlans.Growth,"Growth",6_500,20,"For growing businesses with more staff."),
        new(SubscriptionPlans.Business,"Business",15_000,50,"For established businesses needing more access.")
    ];

    public static void Map(WebApplication app)
    {
        var group=app.MapGroup("/api/subscription").RequireAuthorization("AdminOnly");
        group.MapGet("/",Get);
        group.MapPost("/checkout",Checkout).AddEndpointFilter<ValidationFilter>();
        app.MapPost("/api/payments/payhere/notify",Notify).DisableAntiforgery();
    }

    static async Task<IResult> Get(AppDbContext db,ITenantContext context)
    {
        var tenant=await db.Tenants.SingleAsync(x=>x.Id==context.TenantId);
        var activeUsers=await db.Users.CountAsync(x=>x.IsActive);
        var status=tenant.SubscriptionStatus==SubscriptionStatuses.Trialing&&tenant.TrialEndsAt<=DateTimeOffset.UtcNow?SubscriptionStatuses.Expired:tenant.SubscriptionStatus;
        return Results.Ok(new SubscriptionDto(tenant.SubscriptionPlan,status,tenant.UserLimit,activeUsers,Math.Max(0,tenant.UserLimit-activeUsers),tenant.TrialEndsAt,tenant.SubscriptionEndsAt,Plans.Select(x=>new SubscriptionPlanDto(x.Code,x.Name,x.Price,x.UserLimit,x.Description)).ToList()));
    }

    static async Task<IResult> Checkout(CreateSubscriptionCheckoutRequest request,AppDbContext db,ITenantContext context,IConfiguration config,CancellationToken ct)
    {
        var plan=Plans.SingleOrDefault(x=>x.Code.Equals(request.Plan,StringComparison.OrdinalIgnoreCase));
        if(plan is null)return Results.BadRequest(new{message="Select a valid subscription plan."});
        var merchantId=config["PayHere:MerchantId"]?.Trim();var secret=config["PayHere:MerchantSecret"];
        var publicApiUrl=config["PayHere:PublicApiUrl"]?.TrimEnd('/');var frontendUrl=config["FrontendUrl"]?.TrimEnd('/');
        if(string.IsNullOrWhiteSpace(merchantId)||string.IsNullOrWhiteSpace(secret)||!ValidHttps(publicApiUrl)||string.IsNullOrWhiteSpace(frontendUrl))return Results.Problem("PayHere sandbox is not configured. Add the merchant ID, merchant secret, and public HTTPS API URL.",statusCode:503);
        var tenant=await db.Tenants.SingleAsync(x=>x.Id==context.TenantId,ct);var user=await db.Users.SingleAsync(x=>x.Id==context.UserId,ct);
        var order=new PaymentOrder{TenantId=context.TenantId,OrderId=$"LKS-{Guid.NewGuid():N}",Plan=plan.Code,Amount=plan.Price};db.PaymentOrders.Add(order);await db.SaveChangesAsync(ct);
        var amount=plan.Price.ToString("0.00",CultureInfo.InvariantCulture);var currency="LKR";
        var fields=new Dictionary<string,string>{["merchant_id"]=merchantId,["return_url"]=$"{frontendUrl}/subscription?payment=returned",["cancel_url"]=$"{frontendUrl}/subscription?payment=cancelled",["notify_url"]=$"{publicApiUrl}/api/payments/payhere/notify",["first_name"]=user.FirstName,["last_name"]=user.LastName,["email"]=tenant.Email,["phone"]=request.Phone.Trim(),["address"]=request.Address.Trim(),["city"]=request.City.Trim(),["country"]="Sri Lanka",["order_id"]=order.OrderId,["items"]=$"LankaSaaS {plan.Name} plan",["currency"]=currency,["recurrence"]="1 Month",["duration"]="Forever",["amount"]=amount,["hash"]=Hash($"{merchantId}{order.OrderId}{amount}{currency}{Hash(secret)}")};
        var sandbox=config.GetValue("PayHere:Sandbox",true);return Results.Ok(new PaymentCheckoutDto(sandbox?"https://sandbox.payhere.lk/pay/checkout":"https://www.payhere.lk/pay/checkout",fields));
    }

    static async Task<IResult> Notify(HttpRequest request,AppDbContext db,IConfiguration config,CancellationToken ct)
    {
        var form=await request.ReadFormAsync(ct);string Value(string key)=>form[key].ToString();
        var merchantId=Value("merchant_id");var orderId=Value("order_id");var amountText=Value("payhere_amount");var currency=Value("payhere_currency");var statusCode=Value("status_code");var signature=Value("md5sig").ToUpperInvariant();var secret=config["PayHere:MerchantSecret"]??"";var configuredMerchant=config["PayHere:MerchantId"]??"";
        var expected=Hash($"{merchantId}{orderId}{amountText}{currency}{statusCode}{Hash(secret)}");
        if(string.IsNullOrWhiteSpace(secret)||merchantId!=configuredMerchant||!SecureEquals(expected,signature))return Results.Unauthorized();
        var order=await db.PaymentOrders.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.OrderId==orderId,ct);if(order is null)return Results.NotFound();
        if(!decimal.TryParse(amountText,NumberStyles.Number,CultureInfo.InvariantCulture,out var amount)||amount!=order.Amount||currency!=order.Currency)return Results.BadRequest();
        var paymentId=Value("payment_id");if(string.IsNullOrWhiteSpace(paymentId))paymentId=$"{orderId}:{statusCode}:{signature}";
        await using var tx=await db.Database.BeginTransactionAsync(ct);await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({order.TenantId.ToString()}))",ct);
        if(await db.PaymentTransactions.IgnoreQueryFilters().AnyAsync(x=>x.ProviderPaymentId==paymentId,ct)){await tx.CommitAsync(ct);return Results.Ok();}
        db.PaymentTransactions.Add(new PaymentTransaction{TenantId=order.TenantId,PaymentOrderId=order.Id,ProviderPaymentId=paymentId,Amount=amount,Currency=currency,StatusCode=statusCode,PaymentMethod=Clean(Value("method"))});
        order.Status=Status(statusCode);
        var tenant=await db.Tenants.IgnoreQueryFilters().SingleAsync(x=>x.Id==order.TenantId,ct);
        if(statusCode=="2")
        {
            var plan=Plans.Single(x=>x.Code==order.Plan);tenant.SubscriptionPlan=plan.Code;tenant.SubscriptionStatus=SubscriptionStatuses.Active;tenant.UserLimit=plan.UserLimit;tenant.TrialEndsAt=null;var from=tenant.SubscriptionEndsAt>DateTimeOffset.UtcNow?tenant.SubscriptionEndsAt.Value:DateTimeOffset.UtcNow;tenant.SubscriptionEndsAt=from.AddMonths(1);
        }
        else if(statusCode=="-3"&&tenant.SubscriptionPlan==order.Plan)tenant.SubscriptionStatus=SubscriptionStatuses.PastDue;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok();
    }

    static string Status(string code)=>code switch{"2"=>"Succeeded","0"=>"Pending","-1"=>"Cancelled","-2"=>"Failed","-3"=>"ChargedBack",_=>"Unknown"};
    static string Hash(string value)=>Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value)));
    static bool SecureEquals(string left,string right)=>left.Length==right.Length&&CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left),Encoding.ASCII.GetBytes(right));
    static bool ValidHttps(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps;
    static string? Clean(string value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}
