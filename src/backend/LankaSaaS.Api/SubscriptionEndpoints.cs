using LankaSaaS.Application;
using LankaSaaS.Infrastructure;
using LankaSaaS.Domain;
using Microsoft.EntityFrameworkCore;

public static class SubscriptionEndpoints
{
    static readonly List<SubscriptionPlanDto> Plans =
    [
        new("Starter","Starter",2_500,5,"For small teams getting started."),
        new("Growth","Growth",6_500,20,"For growing businesses with more staff."),
        new("Business","Business",15_000,50,"For established businesses needing more access.")
    ];

    public static void Map(WebApplication app)=>app.MapGet("/api/subscription",Get).RequireAuthorization("AdminOnly");

    static async Task<IResult> Get(AppDbContext db,ITenantContext context)
    {
        var tenant=await db.Tenants.SingleAsync(x=>x.Id==context.TenantId);
        var activeUsers=await db.Users.CountAsync(x=>x.IsActive);
        var status=tenant.SubscriptionStatus==SubscriptionStatuses.Trialing&&tenant.TrialEndsAt<=DateTimeOffset.UtcNow?SubscriptionStatuses.Expired:tenant.SubscriptionStatus;
        return Results.Ok(new SubscriptionDto(tenant.SubscriptionPlan,status,tenant.UserLimit,activeUsers,Math.Max(0,tenant.UserLimit-activeUsers),tenant.TrialEndsAt,tenant.SubscriptionEndsAt,Plans));
    }
}
