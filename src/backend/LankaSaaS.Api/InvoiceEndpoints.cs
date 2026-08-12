using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class InvoiceEndpoints
{
    public static void Map(WebApplication app)
    {
        var g=app.MapGroup("/api/invoices").RequireAuthorization();
        g.MapGet("/",async(AppDbContext db)=>Results.Ok(await db.Invoices.OrderByDescending(x=>x.IssueDate).Select(x=>new InvoiceListDto(x.Id,x.InvoiceNumber,x.CustomerName,x.IssueDate,x.DueDate,x.Status.ToString(),x.Total)).ToListAsync()));
        g.MapGet("/{id:guid}",async(Guid id,AppDbContext db)=>await Load(id,db) is {} x?Results.Ok(x):Results.NotFound());
        g.MapPost("/",Create);
        g.MapPut("/{id:guid}",Update);
        g.MapPatch("/{id:guid}/status",ChangeStatus);
        g.MapDelete("/{id:guid}",async(Guid id,AppDbContext db)=>{var x=await db.Invoices.FindAsync(id);if(x is null)return Results.NotFound();if(x.Status!=InvoiceStatus.Draft)return Results.Conflict(new{message="Only draft invoices can be deleted."});db.Remove(x);await db.SaveChangesAsync();return Results.NoContent();});
        app.MapGet("/api/dashboard",async(AppDbContext db)=>Results.Ok(new DashboardDto(await db.Invoices.Where(x=>x.Status==InvoiceStatus.Paid).SumAsync(x=>(decimal?)x.Total)??0,await db.Expenses.SumAsync(x=>(decimal?)x.Amount)??0,await db.Customers.CountAsync(),await db.Products.CountAsync()))).RequireAuthorization();
    }

    static async Task<IResult> Create(InvoiceRequest r,AppDbContext db,ITenantContext tenant)
    {
        var error=Validate(r); if(error is not null)return error;
        var customer=await db.Customers.SingleOrDefaultAsync(x=>x.Id==r.CustomerId);if(customer is null)return Results.BadRequest(new{message="Customer was not found."});
        if(!await ProductsBelongToTenant(r,db))return Results.BadRequest(new{message="One or more products were not found."});
        var next=(await db.Invoices.CountAsync(x=>x.CreatedAt.Year==DateTimeOffset.UtcNow.Year))+1;
        var invoice=new Invoice{CustomerId=customer.Id,CustomerName=customer.Name,InvoiceNumber=$"INV-{DateTimeOffset.UtcNow.Year}-{next:00000}",IssueDate=r.IssueDate,DueDate=r.DueDate,Notes=r.Notes};
        Apply(invoice,r,tenant.TenantId);db.Invoices.Add(invoice);await db.SaveChangesAsync();return Results.Created($"/api/invoices/{invoice.Id}",await Load(invoice.Id,db));
    }
    static async Task<IResult> Update(Guid id,InvoiceRequest r,AppDbContext db,ITenantContext tenant)
    {
        var error=Validate(r);if(error is not null)return error;var invoice=await db.Invoices.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id);if(invoice is null)return Results.NotFound();if(invoice.Status!=InvoiceStatus.Draft)return Results.Conflict(new{message="Only draft invoices can be edited."});var customer=await db.Customers.SingleOrDefaultAsync(x=>x.Id==r.CustomerId);if(customer is null)return Results.BadRequest(new{message="Customer was not found."});if(!await ProductsBelongToTenant(r,db))return Results.BadRequest(new{message="One or more products were not found."});db.InvoiceItems.RemoveRange(invoice.Items);invoice.CustomerId=customer.Id;invoice.CustomerName=customer.Name;invoice.IssueDate=r.IssueDate;invoice.DueDate=r.DueDate;invoice.Notes=r.Notes;Apply(invoice,r,tenant.TenantId);await db.SaveChangesAsync();return Results.Ok(await Load(id,db));
    }
    static async Task<IResult> ChangeStatus(Guid id,InvoiceStatusRequest r,AppDbContext db)
    {var invoice=await db.Invoices.FindAsync(id);if(invoice is null)return Results.NotFound();if(!Enum.TryParse<InvoiceStatus>(r.Status,true,out var status))return Results.BadRequest(new{message="Invalid invoice status."});var allowed=invoice.Status switch{InvoiceStatus.Draft=>status is InvoiceStatus.Issued or InvoiceStatus.Cancelled,InvoiceStatus.Issued=>status is InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Cancelled,InvoiceStatus.Overdue=>status is InvoiceStatus.Paid or InvoiceStatus.Cancelled,_=>false};if(!allowed)return Results.Conflict(new{message=$"Cannot change an invoice from {invoice.Status} to {status}."});invoice.Status=status;await db.SaveChangesAsync();return Results.NoContent();}
    static IResult? Validate(InvoiceRequest r){if(r.CustomerId==Guid.Empty)return Results.BadRequest(new{message="Customer is required."});if(r.DueDate<r.IssueDate)return Results.BadRequest(new{message="Due date cannot be before issue date."});if(r.Items is null||r.Items.Count==0)return Results.BadRequest(new{message="Add at least one invoice item."});if(r.Items.Any(x=>string.IsNullOrWhiteSpace(x.Description)||x.Quantity<=0||x.UnitPrice<0||x.Discount<0||x.TaxRate is <0 or >100||x.Discount>x.Quantity*x.UnitPrice))return Results.BadRequest(new{message="One or more invoice items are invalid."});return null;}
    static async Task<bool> ProductsBelongToTenant(InvoiceRequest r,AppDbContext db){var ids=r.Items.Where(x=>x.ProductId.HasValue).Select(x=>x.ProductId!.Value).Distinct().ToList();return ids.Count==0||await db.Products.CountAsync(x=>ids.Contains(x.Id))==ids.Count;}
    static void Apply(Invoice invoice,InvoiceRequest r,Guid tenantId){invoice.Items=r.Items.Select(x=>{var sub=Math.Round(x.Quantity*x.UnitPrice,2);var taxable=sub-x.Discount;var tax=Math.Round(taxable*x.TaxRate/100,2);return new InvoiceItem{TenantId=tenantId,ProductId=x.ProductId,Description=x.Description.Trim(),Quantity=x.Quantity,UnitPrice=x.UnitPrice,Discount=x.Discount,TaxRate=x.TaxRate,LineSubtotal=sub,LineTotal=taxable+tax};}).ToList();invoice.Subtotal=invoice.Items.Sum(x=>x.LineSubtotal);invoice.DiscountTotal=invoice.Items.Sum(x=>x.Discount);invoice.TaxTotal=invoice.Items.Sum(x=>x.LineTotal-(x.LineSubtotal-x.Discount));invoice.Total=invoice.Items.Sum(x=>x.LineTotal);}
    static Task<InvoiceDto?> Load(Guid id,AppDbContext db)=>db.Invoices.Where(x=>x.Id==id).Select(x=>new InvoiceDto(x.Id,x.InvoiceNumber,x.CustomerId,x.CustomerName,x.IssueDate,x.DueDate,x.Status.ToString(),x.Subtotal,x.DiscountTotal,x.TaxTotal,x.Total,x.Notes,x.Items.Select(i=>new InvoiceItemDto(i.Id,i.ProductId,i.Description,i.Quantity,i.UnitPrice,i.Discount,i.TaxRate,i.LineSubtotal,i.LineTotal)).ToList(),x.CreatedAt)).SingleOrDefaultAsync();
}
