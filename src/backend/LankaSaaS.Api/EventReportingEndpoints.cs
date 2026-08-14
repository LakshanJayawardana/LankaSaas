using System.Globalization;using System.Text;using LankaSaaS.Application;using LankaSaaS.Domain;using LankaSaaS.Infrastructure;using Microsoft.EntityFrameworkCore;
public static class EventReportingEndpoints
{
 public static void Map(WebApplication app)
 {
  app.MapGet("/api/reports/events",Report).RequireAuthorization();
  app.MapGet("/api/reports/events/export",Export).RequireAuthorization();
  app.MapGet("/api/reports/events/health",()=>Results.Ok(new{module="event-reporting",version=1})).RequireAuthorization();
 }
 static async Task<IResult> Report(DateOnly? from,DateOnly? to,Guid? eventId,AppDbContext db,CancellationToken ct){var result=await Build(from,to,eventId,db,ct);return result.Error is null?Results.Ok(result.Report):result.Error;}
 static async Task<IResult> Export(DateOnly? from,DateOnly? to,Guid? eventId,AppDbContext db,CancellationToken ct){var result=await Build(from,to,eventId,db,ct);if(result.Error is not null)return result.Error;var r=result.Report!;var csv=new StringBuilder("Event,Customer,Status,Start,Budget revenue,Invoiced,Received,Budget cost,Actual cost,Labour,Receivable,Payable,Profit,Margin %\r\n");foreach(var x in r.Events)csv.AppendLine(string.Join(',',Q(x.EventName),Q(x.CustomerName),Q(x.Status),x.StartsAt.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),N(x.BudgetedRevenue),N(x.InvoicedRevenue),N(x.ReceivedRevenue),N(x.BudgetedCost),N(x.ActualCost),N(x.LabourCost),N(x.Receivable),N(x.Payable),N(x.Profit),N(x.MarginPercent)));return Results.Ok(new EventReportExportDto($"event-report-{r.From:yyyyMMdd}-{r.To:yyyyMMdd}.csv",csv.ToString()));}
 static async Task<(EventReportingDto? Report,IResult? Error)> Build(DateOnly? from,DateOnly? to,Guid? eventId,AppDbContext db,CancellationToken ct)
 {
  var today=BusinessClock.Today;
  var start=from??new DateOnly(today.Year,1,1);
  var end=to??new DateOnly(today.Year,12,31);
  if(end<start)return(null,Results.BadRequest(new{message="The end date cannot be before the start date."}));
  var startAt=new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(5.5)).ToUniversalTime();
  var endAt=new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(5.5)).ToUniversalTime();
  var eventQuery=db.Events.AsNoTracking().Where(x=>x.StartsAt>=startAt&&x.StartsAt<endAt);if(eventId.HasValue){if(!await db.Events.AnyAsync(x=>x.Id==eventId.Value,ct))return(null,Results.NotFound());eventQuery=eventQuery.Where(x=>x.Id==eventId.Value);}var events=await eventQuery.OrderBy(x=>x.StartsAt).ToListAsync(ct);
  var ids=events.Select(x=>x.Id).ToList();
  var expenses=await db.Expenses.AsNoTracking().Where(x=>x.EventId.HasValue&&ids.Contains(x.EventId.Value)).ToListAsync(ct);
  var invoices=await db.Invoices.AsNoTracking().Where(x=>x.EventId.HasValue&&ids.Contains(x.EventId.Value)&&x.Status!=InvoiceStatus.Cancelled).ToListAsync(ct);
  var invoiceIds=invoices.Select(x=>x.Id).ToList();
  var receipts=await db.CustomerPayments.AsNoTracking().Where(x=>invoiceIds.Contains(x.InvoiceId)).ToListAsync(ct);
  var orders=await db.PurchaseOrders.AsNoTracking().Where(x=>x.EventId.HasValue&&ids.Contains(x.EventId.Value)&&x.Status!=PurchaseOrderStatuses.Cancelled).ToListAsync(ct);
  var orderIds=orders.Select(x=>x.Id).ToList();
  var supplierPayments=await db.SupplierPayments.AsNoTracking().Where(x=>orderIds.Contains(x.PurchaseOrderId)).ToListAsync(ct);
  var staffing=await db.EventStaffAssignments.AsNoTracking().Where(x=>ids.Contains(x.EventId)&&x.Status!=StaffingStatuses.Cancelled).ToListAsync(ct);
  var allocations=await db.EventResourceAllocations.AsNoTracking().Where(x=>ids.Contains(x.EventId)&&x.Status!=AllocationStatuses.Cancelled).ToListAsync(ct);
  var quotes=await db.EventQuotations.AsNoTracking().Where(x=>ids.Contains(x.EventId)).ToListAsync(ct);
  var totalCapacity=await db.LogisticsResources.Where(x=>x.Status==ResourceStatuses.Available).SumAsync(x=>(int?)x.TotalQuantity,ct)??0;
  var rows=events.Select(ev=>
  {
   var inv=invoices.Where(x=>x.EventId==ev.Id).ToList();
   var invIds=inv.Select(x=>x.Id).ToHashSet();
   var received=receipts.Where(x=>invIds.Contains(x.InvoiceId)).Sum(x=>x.Amount);
   var eventOrders=orders.Where(x=>x.EventId==ev.Id).ToList();
   var eventOrderIds=eventOrders.Select(x=>x.Id).ToHashSet();
   var paid=supplierPayments.Where(x=>eventOrderIds.Contains(x.PurchaseOrderId)).Sum(x=>x.Amount);
   var cost=expenses.Where(x=>x.EventId==ev.Id).Sum(x=>x.Amount);
   var labour=staffing.Where(x=>x.EventId==ev.Id).Sum(x=>x.ActualCost);
   var invoiced=inv.Sum(x=>x.Total);
   var profit=invoiced-cost;
   return new EventReportRowDto(ev.Id,ev.Name,ev.CustomerName,ev.Status,ev.StartsAt,ev.BudgetedRevenue,invoiced,received,ev.BudgetedCost,cost,labour,Math.Max(0,invoiced-received),Math.Max(0,eventOrders.Sum(x=>x.Total)-paid),profit,invoiced==0?0:Math.Round(profit/invoiced*100,2),staffing.Count(x=>x.EventId==ev.Id),allocations.Where(x=>x.EventId==ev.Id).Sum(x=>x.Quantity));
  }).OrderByDescending(x=>x.Profit).ToList();
  var decided=quotes.Count(x=>x.Status is QuotationStatuses.Accepted or QuotationStatuses.Converted or QuotationStatuses.Rejected);
  var won=quotes.Count(x=>x.Status is QuotationStatuses.Accepted or QuotationStatuses.Converted);
  var activeAllocated=allocations.Where(x=>x.Status is AllocationStatuses.Reserved or AllocationStatuses.Dispatched).Sum(x=>x.Quantity);
  var report=new EventReportingDto(start,end,rows.Count,rows.Sum(x=>x.BudgetedRevenue),rows.Sum(x=>x.InvoicedRevenue),rows.Sum(x=>x.ReceivedRevenue),rows.Sum(x=>x.BudgetedCost),rows.Sum(x=>x.ActualCost),rows.Sum(x=>x.Receivable),rows.Sum(x=>x.Payable),rows.Sum(x=>x.Profit),decided==0?0:Math.Round((decimal)won/decided*100,2),totalCapacity==0?0:Math.Round((decimal)activeAllocated/totalCapacity*100,2),staffing.Sum(x=>Math.Round((decimal)(x.ShiftEndsAt-x.ShiftStartsAt).TotalHours,2)*x.HourlyRate),staffing.Sum(x=>x.ActualCost),rows);
  return(report,null);
 }
 static string Q(string value)=>$"\"{value.Replace("\"","\"\"")}\"";static string N(decimal value)=>value.ToString("0.00",CultureInfo.InvariantCulture);
}
