using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        app.MapGet("/api/subscription/access",Access).RequireAuthorization();
        group.MapPost("/checkout",Checkout).AddEndpointFilter<ValidationFilter>();
        group.MapPost("/cancel",Cancel);
        app.MapPost("/api/payments/payhere/notify",Notify).DisableAntiforgery();
    }

    static async Task<IResult> Get(AppDbContext db,ITenantContext context)
    {
        var tenant=await db.Tenants.SingleAsync(x=>x.Id==context.TenantId);
        var activeUsers=await db.Users.CountAsync(x=>x.IsActive);
        var status=tenant.SubscriptionStatus==SubscriptionStatuses.Trialing&&tenant.TrialEndsAt<=DateTimeOffset.UtcNow?SubscriptionStatuses.Expired:tenant.SubscriptionStatus;
        var history=await (from payment in db.PaymentTransactions join order in db.PaymentOrders on payment.PaymentOrderId equals order.Id orderby payment.CreatedAt descending select new BillingTransactionDto(payment.Id,payment.ProviderPaymentId,order.Plan,payment.Amount,payment.Currency,Status(payment.StatusCode),payment.PaymentMethod,payment.CreatedAt)).Take(50).ToListAsync();
        return Results.Ok(new SubscriptionDto(tenant.SubscriptionPlan,status,tenant.UserLimit,activeUsers,Math.Max(0,tenant.UserLimit-activeUsers),tenant.TrialEndsAt,tenant.SubscriptionEndsAt,tenant.CancellationRequestedAt,Plans.Select(x=>new SubscriptionPlanDto(x.Code,x.Name,x.Price,x.UserLimit,x.Description)).ToList(),history));
    }

    static async Task<IResult> Access(AppDbContext db,ITenantContext context)
    {var tenant=await db.Tenants.AsNoTracking().SingleAsync(x=>x.Id==context.TenantId);return Results.Ok(SubscriptionAccess.Evaluate(tenant,DateTimeOffset.UtcNow));}

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
        var merchantId=Value("merchant_id");var orderId=Value("order_id");var amountText=Value("payhere_amount");var currency=Value("payhere_currency");var statusCode=Value("status_code");var messageType=Value("message_type");var signature=Value("md5sig").ToUpperInvariant();var secret=config["PayHere:MerchantSecret"]??"";var configuredMerchant=config["PayHere:MerchantId"]??"";
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
        if(messageType=="RECURRING_STOPPED")
        {tenant.SubscriptionStatus=SubscriptionStatuses.Cancelled;tenant.CancellationRequestedAt??=DateTimeOffset.UtcNow;}
        else if(messageType=="RECURRING_COMPLETE")
        {tenant.SubscriptionStatus=SubscriptionStatuses.Expired;tenant.SubscriptionEndsAt??=DateTimeOffset.UtcNow;}
        else if(statusCode=="2")
        {
            var plan=Plans.Single(x=>x.Code==order.Plan);tenant.SubscriptionPlan=plan.Code;tenant.SubscriptionStatus=SubscriptionStatuses.Active;tenant.UserLimit=plan.UserLimit;tenant.TrialEndsAt=null;tenant.GraceEndsAt=null;tenant.PayHereSubscriptionId=Clean(Value("subscription_id"))??tenant.PayHereSubscriptionId;tenant.CancellationRequestedAt=null;var from=tenant.SubscriptionEndsAt>DateTimeOffset.UtcNow?tenant.SubscriptionEndsAt.Value:DateTimeOffset.UtcNow;tenant.SubscriptionEndsAt=from.AddMonths(1);
        }
        else if(statusCode=="-2"&&tenant.SubscriptionPlan==order.Plan){tenant.SubscriptionStatus=SubscriptionStatuses.PastDue;tenant.GraceEndsAt=DateTimeOffset.UtcNow.AddDays(7);}
        else if(statusCode=="-3"&&tenant.SubscriptionPlan==order.Plan){tenant.SubscriptionStatus=SubscriptionStatuses.PastDue;tenant.GraceEndsAt=DateTimeOffset.UtcNow;}
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok();
    }

    static async Task<IResult> Cancel(AppDbContext db,ITenantContext context,IConfiguration config,IHttpClientFactory clients,CancellationToken ct)
    {
        var tenant=await db.Tenants.SingleAsync(x=>x.Id==context.TenantId,ct);
        if(tenant.SubscriptionStatus!=SubscriptionStatuses.Active)return Results.Conflict(new{message="Only an active subscription can be cancelled."});
        if(string.IsNullOrWhiteSpace(tenant.PayHereSubscriptionId))return Results.Conflict(new{message="The PayHere subscription reference is unavailable. Contact support before cancelling."});
        var appId=config["PayHere:AppId"];var appSecret=config["PayHere:AppSecret"];if(string.IsNullOrWhiteSpace(appId)||string.IsNullOrWhiteSpace(appSecret))return Results.Problem("PayHere subscription management is not configured.",statusCode:503);
        var root=config.GetValue("PayHere:Sandbox",true)?"https://sandbox.payhere.lk":"https://www.payhere.lk";var client=clients.CreateClient();
        using var tokenRequest=new HttpRequestMessage(HttpMethod.Post,$"{root}/merchant/v1/oauth/token"){Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","client_credentials"}})};tokenRequest.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes($"{appId}:{appSecret}")));
        using var tokenResponse=await client.SendAsync(tokenRequest,ct);if(!tokenResponse.IsSuccessStatusCode)return Results.Problem("PayHere authentication failed. Please try again later.",statusCode:502);
        using var tokenJson=JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));if(!tokenJson.RootElement.TryGetProperty("access_token",out var token))return Results.Problem("PayHere returned an invalid authentication response.",statusCode:502);
        if(!long.TryParse(tenant.PayHereSubscriptionId,out var subscriptionId))return Results.Conflict(new{message="The PayHere subscription reference is invalid. Contact support before cancelling."});
        using var cancelRequest=new HttpRequestMessage(HttpMethod.Post,$"{root}/merchant/v1/subscription/cancel"){Content=JsonContent.Create(new{subscription_id=subscriptionId})};cancelRequest.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.GetString());
        using var cancelResponse=await client.SendAsync(cancelRequest,ct);var responseText=await cancelResponse.Content.ReadAsStringAsync(ct);if(!cancelResponse.IsSuccessStatusCode)return Results.Problem("PayHere could not cancel the subscription. Please try again later.",statusCode:502);
        using var cancelJson=JsonDocument.Parse(responseText);if(!cancelJson.RootElement.TryGetProperty("status",out var result)||result.GetInt32()!=1)return Results.Conflict(new{message=cancelJson.RootElement.TryGetProperty("msg",out var message)?message.GetString():"PayHere rejected the cancellation."});
        tenant.SubscriptionStatus=SubscriptionStatuses.Cancelled;tenant.CancellationRequestedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);return Results.Ok(new{message="Subscription cancelled. Access remains available until the current period ends.",endsAt=tenant.SubscriptionEndsAt});
    }

    static string Status(string code)=>code switch{"2"=>"Succeeded","0"=>"Pending","-1"=>"Cancelled","-2"=>"Failed","-3"=>"ChargedBack",_=>"Unknown"};
    static string Hash(string value)=>Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value)));
    static bool SecureEquals(string left,string right)=>left.Length==right.Length&&CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left),Encoding.ASCII.GetBytes(right));
    static bool ValidHttps(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps;
    static string? Clean(string value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}
