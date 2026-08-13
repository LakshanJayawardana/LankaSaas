using LankaSaaS.Application;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class ProfileEndpoints
{
    public static void Map(WebApplication app)
    {
        var group=app.MapGroup("/api/profile").RequireAuthorization();
        group.MapGet("/",Get);
        group.MapPut("/",Update).AddEndpointFilter<ValidationFilter>();
    }

    static async Task<IResult> Get(AppDbContext db,ITenantContext context)
    {
        var user=await db.Users.SingleAsync(x=>x.Id==context.UserId);
        return Results.Ok(Dto(user));
    }

    static async Task<IResult> Update(UpdateUserProfileRequest request,AppDbContext db,ITenantContext context)
    {
        var photoUrl=Clean(request.ProfilePhotoUrl);
        if(photoUrl is not null&&(!Uri.TryCreate(photoUrl,UriKind.Absolute,out var photo)||photo.Scheme!=Uri.UriSchemeHttps))
            return Results.BadRequest(new{message="Profile photo URL must use HTTPS."});
        var user=await db.Users.SingleAsync(x=>x.Id==context.UserId);
        user.ProfilePhotoUrl=photoUrl;
        await db.SaveChangesAsync();
        return Results.Ok(Dto(user));
    }

    static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    static UserProfileDto Dto(LankaSaaS.Domain.User user)=>new(user.Id,user.FirstName,user.LastName,user.Email,user.Role,user.ProfilePhotoUrl);
}
