using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class EventFinanceEndpoints
{
    public static void Map(WebApplication app)
    {
        var g=app.MapGroup("/api/events/{eventId:guid}/finance").RequireAuthorization().AddEndpointFilter<ValidationFilter>();
        g.MapGet("/",Get);
        g.MapPost("/quotations",CreateQuotation);
        g.MapPatch("/quotations/{quotationId:guid}/status/{status}",ChangeQuotationStatus);
        g.MapPost("/quotations/{quotationId:guid}/convert",ConvertToInvoice);
        g.MapPost("/invoices/{invoiceId:guid}/payments",RecordPayment);
    }

    static async Task<IResult> Get(Guid eventId,AppDbContext db,CancellationToken ct)
    {
        var ev=await db.Events.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==eventId,ct);if(ev is null)return Results.NotFound();
        var quotes=await db.EventQuotations.AsNoTracking().Where(x=>x.EventId==eventId).OrderByDescending(x=>x.IssueDate).Select(x=>new EventQuotationDto(x.Id,x.QuotationNumber,x.Status,x.IssueDate,x.ValidUntil,x.Total,x.DepositRequired,x.Notes,x.Items.Select(i=>new QuotationItemRequest(i.Description,i.Quantity,i.UnitPrice)).ToList())).ToListAsync(ct);
        var invoices=await db.Invoices.AsNoTracking().Where(x=>x.EventId==eventId).OrderByDescending(x=>x.IssueDate).Select(x=>new InvoiceListDto(x.Id,x.InvoiceNumber,x.CustomerName,x.IssueDate,x.DueDate,x.Status.ToString(),x.Total)).ToListAsync(ct);
        var invoiced=await db.Invoices.Where(x=>x.EventId==eventId&&x.Status!=InvoiceStatus.Cancelled).SumAsync(x=>(decimal?)x.Total,ct)??0;
        var received=await db.CustomerPayments.Where(x=>x.EventId==eventId).SumAsync(x=>(decimal?)x.Amount,ct)??0;
        var cost=await db.Expenses.Where(x=>x.EventId==eventId).SumAsync(x=>(decimal?)x.Amount,ct)??0;
        var quoted=await db.EventQuotations.Where(x=>x.EventId==eventId&&(x.Status==QuotationStatuses.Accepted||x.Status==QuotationStatuses.Converted)).SumAsync(x=>(decimal?)x.Total,ct)??0;
        return Results.Ok(new EventFinanceDto(ev.Id,ev.Name,quoted,invoiced,received,Math.Max(0,invoiced-received),cost,received-cost,quotes,invoices));
    }

    static async Task<IResult> CreateQuotation(Guid eventId,EventQuotationRequest r,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        if(!await db.Events.AnyAsync(x=>x.Id==eventId,ct))return Results.NotFound();
        if(r.ValidUntil<r.IssueDate||r.Items is null||r.Items.Count==0||r.Items.Any(x=>string.IsNullOrWhiteSpace(x.Description)||x.Quantity<=0||x.UnitPrice<0))return Results.BadRequest(new{message="Check the quotation dates and add at least one valid item."});
        var q=new EventQuotation{TenantId=tenant.TenantId,EventId=eventId,QuotationNumber=$"QUO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",IssueDate=r.IssueDate,ValidUntil=r.ValidUntil,DepositRequired=r.DepositRequired,Notes=r.Notes?.Trim(),Items=r.Items.Select(x=>new EventQuotationItem{TenantId=tenant.TenantId,Description=x.Description.Trim(),Quantity=x.Quantity,UnitPrice=x.UnitPrice,LineTotal=Math.Round(x.Quantity*x.UnitPrice,2)}).ToList()};q.Total=q.Items.Sum(x=>x.LineTotal);if(q.DepositRequired>q.Total)return Results.BadRequest(new{message="Required deposit cannot exceed the quotation total."});db.EventQuotations.Add(q);await db.SaveChangesAsync(ct);return Results.Created($"/api/events/{eventId}/finance",q.Id);
    }

    static async Task<IResult> ChangeQuotationStatus(Guid eventId,Guid quotationId,string status,AppDbContext db,CancellationToken ct)
    {
        var q=await db.EventQuotations.SingleOrDefaultAsync(x=>x.Id==quotationId&&x.EventId==eventId,ct);if(q is null)return Results.NotFound();
        var allowed=q.Status switch{QuotationStatuses.Draft=>status is QuotationStatuses.Sent,QuotationStatuses.Sent=>status is QuotationStatuses.Accepted or QuotationStatuses.Rejected,_=>false};if(!allowed)return Results.Conflict(new{message=$"Cannot change a quotation from {q.Status} to {status}."});q.Status=status;await db.SaveChangesAsync(ct);return Results.NoContent();
    }

    static async Task<IResult> ConvertToInvoice(Guid eventId,Guid quotationId,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        var q=await db.EventQuotations.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==quotationId&&x.EventId==eventId,ct);if(q is null)return Results.NotFound();if(q.Status!=QuotationStatuses.Accepted)return Results.Conflict(new{message="Only an accepted quotation can be converted."});
        var ev=await db.Events.SingleAsync(x=>x.Id==eventId,ct);await using var tx=await db.Database.BeginTransactionAsync(ct);await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({tenant.TenantId.ToString()}))",ct);var company=await db.Tenants.SingleAsync(x=>x.Id==tenant.TenantId,ct);
        var invoice=new Invoice{TenantId=tenant.TenantId,EventId=eventId,QuotationId=q.Id,CustomerId=ev.CustomerId,CustomerName=ev.CustomerName,InvoiceNumber=$"{company.InvoicePrefix}-{company.NextInvoiceNumber:00000}",IssueDate=DateOnly.FromDateTime(DateTime.UtcNow),DueDate=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(company.DefaultPaymentTermsDays)),Subtotal=q.Total,Total=q.Total,Notes=q.Notes,Items=q.Items.Select(x=>new InvoiceItem{TenantId=tenant.TenantId,Description=x.Description,Quantity=x.Quantity,UnitPrice=x.UnitPrice,LineSubtotal=x.LineTotal,LineTotal=x.LineTotal}).ToList()};company.NextInvoiceNumber++;q.Status=QuotationStatuses.Converted;db.Invoices.Add(invoice);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Created($"/api/invoices/{invoice.Id}",new{invoice.Id,invoice.InvoiceNumber});
    }

    static async Task<IResult> RecordPayment(Guid eventId,Guid invoiceId,CustomerPaymentRequest r,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        var invoice=await db.Invoices.SingleOrDefaultAsync(x=>x.Id==invoiceId&&x.EventId==eventId,ct);if(invoice is null)return Results.NotFound();if(invoice.Status==InvoiceStatus.Cancelled)return Results.Conflict(new{message="Payments cannot be recorded against a cancelled invoice."});var paid=await db.CustomerPayments.Where(x=>x.InvoiceId==invoiceId).SumAsync(x=>(decimal?)x.Amount,ct)??0;if(paid+r.Amount>invoice.Total)return Results.BadRequest(new{message="Payment exceeds the outstanding invoice balance."});db.CustomerPayments.Add(new CustomerPayment{TenantId=tenant.TenantId,EventId=eventId,InvoiceId=invoiceId,Amount=r.Amount,PaymentDate=r.PaymentDate,Method=r.Method.Trim(),Reference=r.Reference?.Trim(),IsDeposit=r.IsDeposit});invoice.Status=paid+r.Amount==invoice.Total?InvoiceStatus.Paid:InvoiceStatus.Issued;await db.SaveChangesAsync(ct);return Results.NoContent();
    }
}
