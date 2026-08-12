using LankaSaaS.Application;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class SettingsEndpoints
{
    public static void Map(WebApplication app)
    {
        var g=app.MapGroup("/api/settings").RequireAuthorization();
        g.MapGet("/",Get);
        g.MapPut("/",Update).RequireAuthorization("AdminOnly").AddEndpointFilter<ValidationFilter>();
    }
    static async Task<IResult> Get(AppDbContext db,ITenantContext context){var x=await db.Tenants.SingleAsync(t=>t.Id==context.TenantId);return Results.Ok(Dto(x));}
    static async Task<IResult> Update(UpdateCompanySettingsRequest r,AppDbContext db,ITenantContext context)
    {
        if(!string.IsNullOrWhiteSpace(r.LogoUrl)&&(!Uri.TryCreate(r.LogoUrl,UriKind.Absolute,out var logo)||logo.Scheme!=Uri.UriSchemeHttps))return Results.BadRequest(new{message="Logo URL must use HTTPS."});
        var email=r.Email.Trim().ToLowerInvariant();if(await db.Tenants.AnyAsync(t=>t.Id!=context.TenantId&&t.Email==email))return Results.Conflict(new{message="Another business already uses this email."});
        var x=await db.Tenants.SingleAsync(t=>t.Id==context.TenantId);x.BusinessName=r.BusinessName.Trim();x.Name=x.BusinessName;x.Email=email;x.Phone=Clean(r.Phone);x.Address=Clean(r.Address);x.TaxRegistrationNumber=Clean(r.TaxRegistrationNumber);x.InvoicePrefix=r.InvoicePrefix.Trim().ToUpperInvariant();x.DefaultPaymentTermsDays=r.DefaultPaymentTermsDays;x.DefaultTaxRate=r.DefaultTaxRate;x.InvoiceFooter=Clean(r.InvoiceFooter);x.PaymentInstructions=Clean(r.PaymentInstructions);x.LogoUrl=Clean(r.LogoUrl);await db.SaveChangesAsync();return Results.Ok(Dto(x));
    }
    static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    static CompanySettingsDto Dto(LankaSaaS.Domain.Tenant x)=>new(x.BusinessName,x.Email,x.Phone,x.Address,x.TaxRegistrationNumber,x.InvoicePrefix,x.NextInvoiceNumber,x.DefaultPaymentTermsDays,x.DefaultTaxRate,x.InvoiceFooter,x.PaymentInstructions,x.LogoUrl);
}
