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
        g.MapGet("/",async(AppDbContext db)=>Results.Ok(await db.Users.OrderBy(x=>x.FirstName).ThenBy(x=>x.LastName).Select(x=>new TeamUserDto(x.Id,x.FirstName,x.LastName,x.Email,x.Role,x.IsActive,x.CreatedAt)).ToListAsync()));
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
        user.PasswordHash=hasher.HashPassword(user,r.Password);db.Users.Add(user);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Created($"/api/users/{user.Id}",Dto(user));
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
        var accessChanged=user.Role!=role||user.IsActive!=r.IsActive;user.FirstName=r.FirstName.Trim();user.LastName=r.LastName.Trim();user.Role=role;user.IsActive=r.IsActive;if(accessChanged)await db.RefreshTokens.Where(x=>x.UserId==id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.RevokedAt,DateTimeOffset.UtcNow));await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(Dto(user));
    }

    static async Task<IResult> ResetPassword(Guid id,ResetUserPasswordRequest r,AppDbContext db,IPasswordHasher<User> hasher)
    {var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==id);if(user is null)return Results.NotFound();user.PasswordHash=hasher.HashPassword(user,r.NewPassword);await db.RefreshTokens.Where(x=>x.UserId==id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.RevokedAt,DateTimeOffset.UtcNow));await db.SaveChangesAsync();return Results.NoContent();}
    static bool ValidRole(string value,out string role){if(value.Equals(Roles.Admin,StringComparison.OrdinalIgnoreCase)){role=Roles.Admin;return true;}if(value.Equals(Roles.Staff,StringComparison.OrdinalIgnoreCase)){role=Roles.Staff;return true;}role="";return false;}
    static bool CanAddUser(string status,DateTimeOffset? trialEndsAt)=>status==SubscriptionStatuses.Active||(status==SubscriptionStatuses.Trialing&&trialEndsAt>DateTimeOffset.UtcNow);
    static Task LockTenant(AppDbContext db,Guid tenantId)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({tenantId.ToString()}))");
    static TeamUserDto Dto(User x)=>new(x.Id,x.FirstName,x.LastName,x.Email,x.Role,x.IsActive,x.CreatedAt);
}
