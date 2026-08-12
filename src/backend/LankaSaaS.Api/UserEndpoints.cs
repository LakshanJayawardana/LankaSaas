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
        var email=r.Email.Trim().ToLowerInvariant();
        if(await db.Users.IgnoreQueryFilters().AnyAsync(x=>x.Email==email))return Results.Conflict(new{message="An account with this email already exists."});
        var user=new User{TenantId=tenant.TenantId,FirstName=r.FirstName.Trim(),LastName=r.LastName.Trim(),Email=email,PasswordHash="",Role=role};
        user.PasswordHash=hasher.HashPassword(user,r.Password);db.Users.Add(user);await db.SaveChangesAsync();return Results.Created($"/api/users/{user.Id}",Dto(user));
    }

    static async Task<IResult> Update(Guid id,UpdateTeamUserRequest r,AppDbContext db,ITenantContext tenant)
    {
        if(!ValidRole(r.Role,out var role))return Results.BadRequest(new{message="Role must be Admin or Staff."});
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==id);if(user is null)return Results.NotFound();
        var removesAdmin=user.Role==Roles.Admin&&(!r.IsActive||role!=Roles.Admin);
        if(removesAdmin&&await db.Users.CountAsync(x=>x.Role==Roles.Admin&&x.IsActive)==1)return Results.Conflict(new{message="Your business must always have at least one active Admin."});
        if(user.Id==tenant.UserId&&!r.IsActive)return Results.Conflict(new{message="You cannot deactivate your own account."});
        var accessChanged=user.Role!=role||user.IsActive!=r.IsActive;user.FirstName=r.FirstName.Trim();user.LastName=r.LastName.Trim();user.Role=role;user.IsActive=r.IsActive;if(accessChanged)await db.RefreshTokens.Where(x=>x.UserId==id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.RevokedAt,DateTimeOffset.UtcNow));await db.SaveChangesAsync();return Results.Ok(Dto(user));
    }

    static async Task<IResult> ResetPassword(Guid id,ResetUserPasswordRequest r,AppDbContext db,IPasswordHasher<User> hasher)
    {var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==id);if(user is null)return Results.NotFound();user.PasswordHash=hasher.HashPassword(user,r.NewPassword);await db.RefreshTokens.Where(x=>x.UserId==id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.RevokedAt,DateTimeOffset.UtcNow));await db.SaveChangesAsync();return Results.NoContent();}
    static bool ValidRole(string value,out string role){if(value.Equals(Roles.Admin,StringComparison.OrdinalIgnoreCase)){role=Roles.Admin;return true;}if(value.Equals(Roles.Staff,StringComparison.OrdinalIgnoreCase)){role=Roles.Staff;return true;}role="";return false;}
    static TeamUserDto Dto(User x)=>new(x.Id,x.FirstName,x.LastName,x.Email,x.Role,x.IsActive,x.CreatedAt);
}
