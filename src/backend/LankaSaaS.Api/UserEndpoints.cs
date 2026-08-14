using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public static class UserEndpoints
{
    public static void Map(WebApplication app)
    {
        var g=app.MapGroup("/api/users").RequireAuthorization("AdminOnly");
        g.MapGet("/",async(AppDbContext db,CancellationToken ct)=>Results.Ok(await Dtos(db,ct)));
        g.MapPost("/",Create).AddEndpointFilter<ValidationFilter>();
        g.MapPut("/{id:guid}",Update).AddEndpointFilter<ValidationFilter>();
        g.MapPost("/{id:guid}/reset-password",ResetPassword).AddEndpointFilter<ValidationFilter>();
    }

    static async Task<IResult> Create(CreateTeamUserRequest r,AppDbContext db,ITenantContext tenant,IPasswordHasher<User> hasher)
    {
        if(!ValidRole(r.Role,out var role))return Results.BadRequest(new{message="Role must be Admin or Staff."});
        await using var tx=await db.Database.BeginTransactionAsync();await LockTenant(db,tenant.TenantId);
        var subscription=await db.Tenants.Where(x=>x.Id==tenant.TenantId).Select(x=>new{x.UserLimit,x.SubscriptionStatus,x.TrialEndsAt}).SingleAsync();
        if(!CanAddUser(subscription.SubscriptionStatus,subscription.TrialEndsAt))return Results.Conflict(new{message="Your subscription is not active. Update your subscription before adding users."});
        if(await db.Users.CountAsync(x=>x.IsActive)>=subscription.UserLimit)return Results.Conflict(new{message=$"Your plan allows {subscription.UserLimit} active users. Upgrade your subscription or deactivate a user first."});
        var email=r.Email.Trim().ToLowerInvariant();
        if(await db.Users.IgnoreQueryFilters().AnyAsync(x=>x.Email==email))return Results.Conflict(new{message="An account with this email already exists."});
        var user=new User{TenantId=tenant.TenantId,FirstName=r.FirstName.Trim(),LastName=r.LastName.Trim(),Email=email,PasswordHash="",Role=role};
        user.PasswordHash=hasher.HashPassword(user,r.Password);db.Users.Add(user);var department=await db.Departments.SingleAsync(x=>x.Code==(role==Roles.Admin?"ADMINISTRATION":"GENERAL"));db.UserDepartments.Add(new UserDepartment{UserId=user.Id,DepartmentId=department.Id,AccessLevel=role==Roles.Admin?DepartmentAccessLevels.Manager:DepartmentAccessLevels.Viewer,IsPrimary=true});await db.SaveChangesAsync();await tx.CommitAsync();return Results.Created($"/api/users/{user.Id}",(await Dtos(db,default,user.Id)).Single());
    }

    static async Task<IResult> Update(Guid id,UpdateTeamUserRequest r,AppDbContext db,ITenantContext tenant)
    {
        if(!ValidRole(r.Role,out var role))return Results.BadRequest(new{message="Role must be Admin or Staff."});
        await using var tx=await db.Database.BeginTransactionAsync();await LockTenant(db,tenant.TenantId);
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==id);if(user is null)return Results.NotFound();
        if(!user.IsActive&&r.IsActive){var subscription=await db.Tenants.Where(x=>x.Id==tenant.TenantId).Select(x=>new{x.UserLimit,x.SubscriptionStatus,x.TrialEndsAt}).SingleAsync();if(!CanAddUser(subscription.SubscriptionStatus,subscription.TrialEndsAt))return Results.Conflict(new{message="Your subscription is not active. Update your subscription before reactivating users."});if(await db.Users.CountAsync(x=>x.IsActive)>=subscription.UserLimit)return Results.Conflict(new{message=$"Your plan allows {subscription.UserLimit} active users. Upgrade your subscription or deactivate a user first."});}
        var removesAdmin=user.Role==Roles.Admin&&(!r.IsActive||role!=Roles.Admin);
        if(removesAdmin&&await db.Users.CountAsync(x=>x.Role==Roles.Admin&&x.IsActive)==1)return Results.Conflict(new{message="Your business must always have at least one active Admin."});
        if(user.Id==tenant.UserId&&!r.IsActive)return Results.Conflict(new{message="You cannot deactivate your own account."});
        if(user.Role==Roles.Admin&&role==Roles.Staff)
        {
            var administrationId=await db.Departments.Where(x=>x.Code=="ADMINISTRATION").Select(x=>x.Id).SingleAsync();var memberships=await db.UserDepartments.Where(x=>x.UserId==id).ToListAsync();var administration=memberships.Where(x=>x.DepartmentId==administrationId).ToList();db.UserDepartments.RemoveRange(administration);await db.SaveChangesAsync();var remaining=memberships.Except(administration).ToList();if(remaining.Count==0){var generalId=await db.Departments.Where(x=>x.Code=="GENERAL").Select(x=>x.Id).SingleAsync();db.UserDepartments.Add(new UserDepartment{UserId=id,DepartmentId=generalId,AccessLevel=DepartmentAccessLevels.Viewer,IsPrimary=true});}else if(administration.Any(x=>x.IsPrimary)){remaining[0].IsPrimary=true;}
        }
        var accessChanged=user.Role!=role||user.IsActive!=r.IsActive;user.FirstName=r.FirstName.Trim();user.LastName=r.LastName.Trim();user.Role=role;user.IsActive=r.IsActive;if(accessChanged){user.AccessVersion++;await db.RefreshTokens.Where(x=>x.UserId==id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.RevokedAt,DateTimeOffset.UtcNow));}await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok((await Dtos(db,default,id)).Single());
    }

    static async Task<IResult> ResetPassword(Guid id,ResetUserPasswordRequest r,AppDbContext db,IPasswordHasher<User> hasher)
    {var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==id);if(user is null)return Results.NotFound();user.PasswordHash=hasher.HashPassword(user,r.NewPassword);user.AccessVersion++;await db.RefreshTokens.Where(x=>x.UserId==id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.RevokedAt,DateTimeOffset.UtcNow));await db.SaveChangesAsync();return Results.NoContent();}
    static bool ValidRole(string value,out string role){if(value.Equals(Roles.Admin,StringComparison.OrdinalIgnoreCase)){role=Roles.Admin;return true;}if(value.Equals(Roles.Staff,StringComparison.OrdinalIgnoreCase)){role=Roles.Staff;return true;}role="";return false;}
    static bool CanAddUser(string status,DateTimeOffset? trialEndsAt)=>status==SubscriptionStatuses.Active||status==SubscriptionStatuses.PastDue||status==SubscriptionStatuses.Cancelled||(status==SubscriptionStatuses.Trialing&&trialEndsAt>DateTimeOffset.UtcNow);
    static Task LockTenant(AppDbContext db,Guid tenantId)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({tenantId.ToString()}))");
    static async Task<List<TeamUserDto>> Dtos(AppDbContext db,CancellationToken ct,Guid? userId=null)
    {
        var users=await db.Users.AsNoTracking().Where(x=>!userId.HasValue||x.Id==userId).OrderBy(x=>x.FirstName).ThenBy(x=>x.LastName).ToListAsync(ct);var ids=users.Select(x=>x.Id).ToList();
        var memberships=await (from membership in db.UserDepartments.AsNoTracking() join department in db.Departments.AsNoTracking() on membership.DepartmentId equals department.Id where ids.Contains(membership.UserId) select new{membership.UserId,Dto=new DepartmentMembershipDto(department.Id,department.Name,department.Code,membership.AccessLevel,membership.IsPrimary)}).ToListAsync(ct);
        return users.Select(x=>new TeamUserDto(x.Id,x.FirstName,x.LastName,x.Email,x.Role,x.IsActive,x.CreatedAt,x.ProfilePhotoUrl,memberships.Where(m=>m.UserId==x.Id).Select(m=>m.Dto).OrderByDescending(m=>m.IsPrimary).ThenBy(m=>m.DepartmentName).ToList())).ToList();
    }
}
