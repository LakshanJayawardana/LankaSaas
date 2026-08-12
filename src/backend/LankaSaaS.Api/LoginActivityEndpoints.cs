using LankaSaaS.Application;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class LoginActivityEndpoints
{
    public static void Map(WebApplication app)=>app.MapGet("/api/login-activity",Get).RequireAuthorization("AdminOnly");
    static async Task<IResult> Get(AppDbContext db)
    {
        var since=DateTimeOffset.UtcNow.AddDays(-30);
        var users=await db.Users.OrderByDescending(x=>x.LastLoginAt).Select(x=>new LoginActivityUserDto(x.Id,x.FirstName+" "+x.LastName,x.Email,x.LoginCount,x.LastLoginAt)).ToListAsync();
        var events=await db.LoginEvents.OrderByDescending(x=>x.CreatedAt).Take(50).ToListAsync();
        var names=users.ToDictionary(x=>x.UserId);
        var recent=events.Select(x=>{names.TryGetValue(x.UserId,out var u);return new RecentLoginDto(x.Id,x.UserId,u?.Name??"Unknown user",u?.Email??"",x.CreatedAt);}).ToList();
        var last30=await db.LoginEvents.CountAsync(x=>x.CreatedAt>=since);var active=await db.LoginEvents.Where(x=>x.CreatedAt>=since).Select(x=>x.UserId).Distinct().CountAsync();
        return Results.Ok(new LoginActivityDto(users.Sum(x=>x.TotalLogins),last30,active,users,recent));
    }
}
