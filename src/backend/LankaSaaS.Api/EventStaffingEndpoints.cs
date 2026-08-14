using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

public static class EventStaffingEndpoints
{
    const double EarthRadiusMeters=6371000;

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/staffing/team",async(AppDbContext db,CancellationToken ct)=>Results.Ok(await db.Users.Where(x=>x.IsActive).OrderBy(x=>x.FirstName).Select(x=>new StaffingUserDto(x.Id,x.FirstName+" "+x.LastName,x.Role)).ToListAsync(ct))).RequireAuthorization(Permissions.StaffingManage);
        var g=app.MapGroup("/api/events/{eventId:guid}/staffing");
        g.MapGet("/",Get).RequireAuthorization(Permissions.StaffingView);
        g.MapPost("/",Assign).AddEndpointFilter<ValidationFilter>().RequireAuthorization(Permissions.StaffingManage);
        g.MapPost("/{assignmentId:guid}/check-in",CheckIn).AddEndpointFilter<ValidationFilter>().RequireAuthorization(Permissions.AttendanceSelf);
        g.MapPost("/{assignmentId:guid}/check-out",CheckOut).AddEndpointFilter<ValidationFilter>().RequireAuthorization(Permissions.AttendanceSelf);
        g.MapPatch("/{assignmentId:guid}/cancel",Cancel).RequireAuthorization(Permissions.StaffingManage);
        g.MapGet("/attendance-attempts",Attempts).RequireAuthorization(Permissions.AttendanceOverride);
    }

    static async Task<IResult> Get(Guid eventId,HttpContext http,AppDbContext db,ITenantContext tenant,IAuthorizationService authorization,CancellationToken ct)
    {
        var ev=await db.Events.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==eventId,ct);if(ev is null)return Results.NotFound();
        var query=db.EventStaffAssignments.Where(x=>x.EventId==eventId);
        if(!(await authorization.AuthorizeAsync(http.User,Permissions.StaffingManage)).Succeeded)query=query.Where(x=>x.UserId==tenant.UserId);
        var rows=await query.OrderBy(x=>x.ShiftStartsAt).ToListAsync(ct);
        var policy=new EventAttendancePolicyDto(ev.RequireLocationForAttendance,ev.Latitude,ev.Longitude,ev.AttendanceRadiusMeters,ev.MaximumLocationAccuracyMeters,ev.CheckInWindowMinutes);
        return Results.Ok(new EventStaffingDto(ev.Id,ev.Name,rows.Where(x=>x.Status!=StaffingStatuses.Cancelled).Sum(x=>PlannedHours(x)*x.HourlyRate),rows.Sum(x=>x.ActualCost),policy,rows.Select(Dto).ToList()));
    }

    static async Task<IResult> Assign(Guid eventId,StaffAssignmentRequest r,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        if(r.ShiftEndsAt<=r.ShiftStartsAt)return Results.BadRequest(new{message="Shift end must be after its start."});
        var ev=await db.Events.SingleOrDefaultAsync(x=>x.Id==eventId,ct);if(ev is null)return Results.NotFound();
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==r.UserId&&x.IsActive,ct);if(user is null)return Results.BadRequest(new{message="Select an active team member."});
        var conflict=await (from assignment in db.EventStaffAssignments join assignedEvent in db.Events on assignment.EventId equals assignedEvent.Id where assignment.UserId==r.UserId&&assignment.Status!=StaffingStatuses.Cancelled&&assignment.ShiftStartsAt<r.ShiftEndsAt&&r.ShiftStartsAt<assignment.ShiftEndsAt select new{assignedEvent.Name,assignment.ShiftStartsAt,assignment.ShiftEndsAt}).OrderBy(x=>x.ShiftStartsAt).FirstOrDefaultAsync(ct);if(conflict is not null)return Results.Conflict(new{message=$"This team member is already assigned to {conflict.Name} from {conflict.ShiftStartsAt:yyyy-MM-dd HH:mm} UTC to {conflict.ShiftEndsAt:yyyy-MM-dd HH:mm} UTC. Change the shift or cancel the existing assignment."});
        var x=new EventStaffAssignment{TenantId=tenant.TenantId,EventId=eventId,UserId=user.Id,StaffName=user.FirstName+" "+user.LastName,Responsibility=r.Responsibility.Trim(),ShiftStartsAt=r.ShiftStartsAt,ShiftEndsAt=r.ShiftEndsAt,HourlyRate=r.HourlyRate,Notes=Clean(r.Notes)};
        db.EventStaffAssignments.Add(x);await db.SaveChangesAsync(ct);return Results.Created($"/api/events/{eventId}/staffing/{x.Id}",Dto(x));
    }

    static async Task<IResult> CheckIn(Guid eventId,Guid assignmentId,AttendanceRequest r,HttpContext http,AppDbContext db,ITenantContext tenant,IAuthorizationService authorization,CancellationToken ct)
    {
        var loaded=await Load(eventId,assignmentId,db,ct);if(loaded is null)return Results.NotFound();var (ev,x)=loaded.Value;
        var isOverride=await IsOverride(r,http,authorization);if(x.UserId!=tenant.UserId&&!isOverride)return await Reject(ev,x,r,tenant,"check-in","You can record attendance only for your own assignment. A permitted override requires a reason.",StatusCodes.Status403Forbidden,false,db,ct);
        if(x.Status!=StaffingStatuses.Scheduled)return await Reject(ev,x,r,tenant,"check-in","Only scheduled staff can check in.",StatusCodes.Status409Conflict,isOverride,db,ct);
        var now=DateTimeOffset.UtcNow;
        if(!isOverride&&(now<x.ShiftStartsAt.AddMinutes(-ev.CheckInWindowMinutes)||now>x.ShiftEndsAt))return await Reject(ev,x,r,tenant,"check-in",$"Check-in is allowed from {ev.CheckInWindowMinutes} minutes before the shift until the shift ends.",StatusCodes.Status409Conflict,isOverride,db,ct);
        var geo=ValidateLocation(ev,r,isOverride);if(geo.Error is not null)return await Reject(ev,x,r,tenant,"check-in",geo.Error,geo.StatusCode,isOverride,db,ct,geo.Distance);
        x.CheckedInAt=now;x.Status=StaffingStatuses.CheckedIn;db.AttendanceAttempts.Add(Attempt(ev,x,r,tenant,"check-in",true,isOverride,null,geo.Distance));await db.SaveChangesAsync(ct);return Results.Ok(Dto(x));
    }

    static async Task<IResult> CheckOut(Guid eventId,Guid assignmentId,AttendanceRequest r,HttpContext http,AppDbContext db,ITenantContext tenant,IAuthorizationService authorization,CancellationToken ct)
    {
        var loaded=await Load(eventId,assignmentId,db,ct);if(loaded is null)return Results.NotFound();var (ev,x)=loaded.Value;
        var isOverride=await IsOverride(r,http,authorization);if(x.UserId!=tenant.UserId&&!isOverride)return await Reject(ev,x,r,tenant,"check-out","You can record attendance only for your own assignment. A permitted override requires a reason.",StatusCodes.Status403Forbidden,false,db,ct);
        if(x.Status!=StaffingStatuses.CheckedIn||x.CheckedInAt is null)return await Reject(ev,x,r,tenant,"check-out","The team member must be checked in first.",StatusCodes.Status409Conflict,isOverride,db,ct);
        var geo=ValidateLocation(ev,r,isOverride);if(geo.Error is not null)return await Reject(ev,x,r,tenant,"check-out",geo.Error,geo.StatusCode,isOverride,db,ct,geo.Distance);
        var now=DateTimeOffset.UtcNow;if(now<=x.CheckedInAt)return await Reject(ev,x,r,tenant,"check-out","Check-out must be after check-in.",StatusCodes.Status409Conflict,isOverride,db,ct,geo.Distance);
        await using var tx=await db.Database.BeginTransactionAsync(ct);x.CheckedOutAt=now;x.ActualHours=Math.Round((decimal)(now-x.CheckedInAt.Value).TotalHours,2);x.ActualCost=Math.Round(x.ActualHours*x.HourlyRate,2);x.Status=StaffingStatuses.Completed;
        if(x.ActualCost>0){var date=BusinessClock.FromInstant(now);db.Expenses.Add(new Expense{TenantId=tenant.TenantId,EventId=eventId,EventStaffAssignmentId=x.Id,Description=$"Labour: {x.StaffName} - {x.Responsibility}",Category="Labour",Amount=x.ActualCost,ExpenseDate=date});await AccountingService.Post(db,tenant,date,$"Labour for {x.StaffName}","StaffAssignment",x.Id,eventId,null,[(SystemAccountCodes.EventExpenses,x.ActualCost,0),(SystemAccountCodes.Payables,0,x.ActualCost)],ct);}
        db.AttendanceAttempts.Add(Attempt(ev,x,r,tenant,"check-out",true,isOverride,null,geo.Distance));await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(Dto(x));
    }

    static async Task<IResult> Cancel(Guid eventId,Guid assignmentId,AppDbContext db,CancellationToken ct){var x=await db.EventStaffAssignments.SingleOrDefaultAsync(x=>x.Id==assignmentId&&x.EventId==eventId,ct);if(x is null)return Results.NotFound();if(x.Status!=StaffingStatuses.Scheduled)return Results.Conflict(new{message="Only a scheduled assignment can be cancelled."});x.Status=StaffingStatuses.Cancelled;await db.SaveChangesAsync(ct);return Results.NoContent();}

    static async Task<IResult> Attempts(Guid eventId,AppDbContext db,CancellationToken ct)
    {
        if(!await db.Events.AnyAsync(x=>x.Id==eventId,ct))return Results.NotFound();
        var rows=await db.AttendanceAttempts.Where(x=>x.EventId==eventId).OrderByDescending(x=>x.CreatedAt).Take(200).Select(x=>new{x.Id,x.EventStaffAssignmentId,x.StaffUserId,x.RequestedByUserId,x.Action,x.Latitude,x.Longitude,x.AccuracyMeters,x.DistanceMeters,x.IsAccepted,x.IsOverride,x.OverrideReason,x.FailureReason,x.CreatedAt}).ToListAsync(ct);
        return Results.Ok(rows);
    }

    static async Task<bool> IsOverride(AttendanceRequest request,HttpContext http,IAuthorizationService authorization)=>request.IsOverride&&!string.IsNullOrWhiteSpace(request.OverrideReason)&&(await authorization.AuthorizeAsync(http.User,Permissions.AttendanceOverride)).Succeeded;

    static (double? Distance,string? Error,int StatusCode) ValidateLocation(BusinessEvent ev,AttendanceRequest r,bool isOverride)
    {
        if(isOverride||!ev.RequireLocationForAttendance)return(null,null,StatusCodes.Status200OK);
        if(ev.Latitude is null||ev.Longitude is null)return(null,"This event does not have a valid attendance location. Ask an administrator for help.",StatusCodes.Status409Conflict);
        if(r.Latitude is null||r.Longitude is null||r.AccuracyMeters is null)return(null,"Location permission is required to record attendance.",StatusCodes.Status422UnprocessableEntity);
        if(r.Latitude is < -90 or > 90||r.Longitude is < -180 or > 180)return(null,"The device returned invalid location coordinates.",StatusCodes.Status422UnprocessableEntity);
        if(r.AccuracyMeters<0||r.AccuracyMeters>ev.MaximumLocationAccuracyMeters)return(null,$"Location accuracy must be within {ev.MaximumLocationAccuracyMeters} metres. Move to an open area and try again.",StatusCodes.Status422UnprocessableEntity);
        var distance=Distance(ev.Latitude.Value,ev.Longitude.Value,r.Latitude.Value,r.Longitude.Value);
        if(distance>ev.AttendanceRadiusMeters)return(distance,$"You are approximately {Math.Round(distance)} metres from the event. Move within {ev.AttendanceRadiusMeters} metres to record attendance.",StatusCodes.Status403Forbidden);
        return(distance,null,StatusCodes.Status200OK);
    }

    static async Task<IResult> Reject(BusinessEvent ev,EventStaffAssignment x,AttendanceRequest r,ITenantContext tenant,string action,string message,int status,bool isOverride,AppDbContext db,CancellationToken ct,double? distance=null)
    {
        var attempt=Attempt(ev,x,r,tenant,action,false,isOverride,message,distance);db.AttendanceAttempts.Add(attempt);await db.SaveChangesAsync(ct);
        return Results.Json(new{message},statusCode:status);
    }

    static AttendanceAttempt Attempt(BusinessEvent ev,EventStaffAssignment x,AttendanceRequest r,ITenantContext tenant,string action,bool accepted,bool isOverride,string? failure,double? distance)=>new(){TenantId=tenant.TenantId,EventId=ev.Id,EventStaffAssignmentId=x.Id,StaffUserId=x.UserId,RequestedByUserId=tenant.UserId,Action=action,Latitude=r.Latitude,Longitude=r.Longitude,AccuracyMeters=r.AccuracyMeters,DistanceMeters=distance,IsAccepted=accepted,IsOverride=isOverride,OverrideReason=isOverride?Clean(r.OverrideReason):null,FailureReason=failure};

    static async Task<(BusinessEvent Event,EventStaffAssignment Assignment)?> Load(Guid eventId,Guid assignmentId,AppDbContext db,CancellationToken ct){var ev=await db.Events.SingleOrDefaultAsync(x=>x.Id==eventId,ct);if(ev is null)return null;var assignment=await db.EventStaffAssignments.SingleOrDefaultAsync(x=>x.Id==assignmentId&&x.EventId==eventId,ct);return assignment is null?null:(ev,assignment);}
    static double Distance(double lat1,double lon1,double lat2,double lon2){static double Rad(double x)=>x*Math.PI/180;var dLat=Rad(lat2-lat1);var dLon=Rad(lon2-lon1);var a=Math.Sin(dLat/2)*Math.Sin(dLat/2)+Math.Cos(Rad(lat1))*Math.Cos(Rad(lat2))*Math.Sin(dLon/2)*Math.Sin(dLon/2);a=Math.Clamp(a,0,1);return EarthRadiusMeters*2*Math.Atan2(Math.Sqrt(a),Math.Sqrt(1-a));}
    static decimal PlannedHours(EventStaffAssignment x)=>Math.Round((decimal)(x.ShiftEndsAt-x.ShiftStartsAt).TotalHours,2);
    static StaffAssignmentDto Dto(EventStaffAssignment x)=>new(x.Id,x.EventId,x.UserId,x.StaffName,x.Responsibility,x.ShiftStartsAt,x.ShiftEndsAt,x.HourlyRate,PlannedHours(x),Math.Round(PlannedHours(x)*x.HourlyRate,2),x.Status,x.CheckedInAt,x.CheckedOutAt,x.ActualHours,x.ActualCost,x.Notes);
    static string? Clean(string? x)=>string.IsNullOrWhiteSpace(x)?null:x.Trim();
}
