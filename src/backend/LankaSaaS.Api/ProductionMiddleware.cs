using System.Diagnostics;using LankaSaaS.Application;using LankaSaaS.Domain;using LankaSaaS.Infrastructure;using Microsoft.EntityFrameworkCore;
public sealed class ProductionMiddleware(RequestDelegate next,ILogger<ProductionMiddleware> logger)
{
 public async Task InvokeAsync(HttpContext http,AppDbContext db,ITenantContext tenant)
 {
  var correlation=http.Request.Headers["X-Correlation-ID"].FirstOrDefault();if(string.IsNullOrWhiteSpace(correlation)||correlation.Length>100)correlation=Guid.NewGuid().ToString("N");http.Response.Headers["X-Correlation-ID"]=correlation;http.Response.Headers["X-Content-Type-Options"]="nosniff";http.Response.Headers["X-Frame-Options"]="DENY";http.Response.Headers["Referrer-Policy"]="no-referrer";http.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
  using var scope=logger.BeginScope(new Dictionary<string,object>{{"CorrelationId",correlation},{"RequestPath",http.Request.Path.Value??""}});var watch=Stopwatch.StartNew();await next(http);watch.Stop();logger.LogInformation("HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs}ms",http.Request.Method,http.Request.Path,http.Response.StatusCode,watch.ElapsedMilliseconds);
  if(tenant.IsAuthenticated&&!IsRead(http.Request.Method)&&http.Response.StatusCode<500&&!http.Request.Path.StartsWithSegments("/api/audit")){try{db.AuditEvents.Add(new AuditEvent{TenantId=tenant.TenantId,UserId=tenant.UserId,Method=http.Request.Method,Path=http.Request.Path.Value??"/",StatusCode=http.Response.StatusCode,CorrelationId=correlation,IpAddress=http.Connection.RemoteIpAddress?.ToString()});await db.SaveChangesAsync(http.RequestAborted);}catch(Exception ex){logger.LogError(ex,"Could not persist audit event for {Method} {Path}",http.Request.Method,http.Request.Path);}}
 }
 static bool IsRead(string method)=>HttpMethods.IsGet(method)||HttpMethods.IsHead(method)||HttpMethods.IsOptions(method);
}
public static class AuditEndpoints
{
 public static void Map(WebApplication app)=>app.MapGet("/api/audit",async(int? take,AppDbContext db,CancellationToken ct)=>Results.Ok(await db.AuditEvents.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(Math.Clamp(take??100,1,500)).Select(x=>new AuditEventDto(x.Id,x.UserId,x.Method,x.Path,x.StatusCode,x.CorrelationId,x.IpAddress,x.CreatedAt)).ToListAsync(ct))).RequireAuthorization("AdminOnly");
}
