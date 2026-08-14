using System.Diagnostics;
using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public sealed class ProductionMiddleware(RequestDelegate next,ILogger<ProductionMiddleware> logger)
{
 public async Task InvokeAsync(HttpContext http,AppDbContext db,ITenantContext tenant)
 {
  var correlation=http.Request.Headers["X-Correlation-ID"].FirstOrDefault();if(string.IsNullOrWhiteSpace(correlation)||correlation.Length>100)correlation=Guid.NewGuid().ToString("N");http.TraceIdentifier=correlation;http.Response.Headers["X-Correlation-ID"]=correlation;http.Response.Headers["X-Content-Type-Options"]="nosniff";http.Response.Headers["X-Frame-Options"]="DENY";http.Response.Headers["Referrer-Policy"]="no-referrer";http.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
  using var scope=logger.BeginScope(new Dictionary<string,object>{{"CorrelationId",correlation},{"RequestPath",http.Request.Path.Value??""}});var watch=Stopwatch.StartNew();Exception? failure=null;
  try{await next(http);}catch(Exception ex){failure=ex;throw;}finally
  {
   watch.Stop();var status=failure switch{BadHttpRequestException=>StatusCodes.Status400BadRequest,UnauthorizedAccessException=>StatusCodes.Status401Unauthorized,null=>http.Response.StatusCode,_=>StatusCodes.Status500InternalServerError};
   if(failure is not null&&status>=500)logger.LogError(failure,"HTTP {Method} {Path} failed with {StatusCode} after {ElapsedMs}ms",http.Request.Method,http.Request.Path,status,watch.ElapsedMilliseconds);
   else if(failure is not null)logger.LogWarning(failure,"HTTP {Method} {Path} failed with {StatusCode} after {ElapsedMs}ms",http.Request.Method,http.Request.Path,status,watch.ElapsedMilliseconds);
   else if(status>=500)logger.LogError("HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs}ms",http.Request.Method,http.Request.Path,status,watch.ElapsedMilliseconds);
   else if(status>=400)logger.LogWarning("HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs}ms",http.Request.Method,http.Request.Path,status,watch.ElapsedMilliseconds);
   else logger.LogInformation("HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs}ms",http.Request.Method,http.Request.Path,status,watch.ElapsedMilliseconds);
  }
  if(tenant.IsAuthenticated&&!IsRead(http.Request.Method)&&http.Response.StatusCode<500&&!http.Request.Path.StartsWithSegments("/api/audit")){try{db.AuditEvents.Add(new AuditEvent{TenantId=tenant.TenantId,UserId=tenant.UserId,Method=http.Request.Method,Path=http.Request.Path.Value??"/",StatusCode=http.Response.StatusCode,CorrelationId=correlation,IpAddress=http.Connection.RemoteIpAddress?.ToString()});await db.SaveChangesAsync(http.RequestAborted);}catch(Exception ex){logger.LogError(ex,"Could not persist audit event for {Method} {Path}",http.Request.Method,http.Request.Path);}}
 }
 static bool IsRead(string method)=>HttpMethods.IsGet(method)||HttpMethods.IsHead(method)||HttpMethods.IsOptions(method);
}

public sealed class ApiExceptionHandler:IExceptionHandler
{
 public async ValueTask<bool> TryHandleAsync(HttpContext http,Exception exception,CancellationToken ct)
 {
  var status=exception switch{BadHttpRequestException=>StatusCodes.Status400BadRequest,UnauthorizedAccessException=>StatusCodes.Status401Unauthorized,_=>StatusCodes.Status500InternalServerError};
  http.Response.StatusCode=status;
  await http.Response.WriteAsJsonAsync(new ProblemDetails{Status=status,Title=status==400?"Invalid request":status==401?"Unauthorized":"Request failed",Detail=status==400?"The request body or parameters are invalid.":status==401?"Authentication is required.":"An unexpected error occurred. Contact support with the correlation ID.",Instance=http.Request.Path,Extensions={{"correlationId",http.TraceIdentifier}}},ct);return true;
 }
}

public static class AuditEndpoints
{
 public static void Map(WebApplication app)=>app.MapGet("/api/audit",async(int? take,AppDbContext db,CancellationToken ct)=>Results.Ok(await db.AuditEvents.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(Math.Clamp(take??100,1,500)).Select(x=>new AuditEventDto(x.Id,x.UserId,x.Method,x.Path,x.StatusCode,x.CorrelationId,x.IpAddress,x.CreatedAt)).ToListAsync(ct))).RequireAuthorization("AdminOnly");
}
