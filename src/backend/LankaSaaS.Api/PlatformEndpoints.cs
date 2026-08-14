using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public static class PlatformAuthentication
{
    public const string Scheme = "PlatformBearer";
}

public sealed class PlatformTokenService(IConfiguration configuration)
{
    public PlatformAuthResponse Create(PlatformUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(configuration.GetValue("PlatformJwt:AccessMinutes", 15));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("access_version", user.AccessVersion.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("identity_scope", "platform")
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["PlatformJwt:Key"]!));
        var token = new JwtSecurityToken(configuration["PlatformJwt:Issuer"], configuration["PlatformJwt:Audience"], claims, now.UtcDateTime, expires.UtcDateTime, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Email, user.Role);
    }
}

public static class PlatformBootstrap
{
    public static async Task EnsureOwnerAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["PlatformAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["PlatformAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("PlatformAdmin bootstrap requires a valid email and a password containing at least 12 characters.");

        var db = services.GetRequiredService<AppDbContext>();
        if (await db.PlatformUsers.AnyAsync()) return;
        var owner = new PlatformUser { Email = email, PasswordHash = string.Empty };
        owner.PasswordHash = services.GetRequiredService<IPasswordHasher<PlatformUser>>().HashPassword(owner, password);
        db.PlatformUsers.Add(owner);
        await db.SaveChangesAsync();
    }
}

public static class PlatformEndpoints
{
    static readonly string[] Plans = [SubscriptionPlans.Trial, SubscriptionPlans.Starter, SubscriptionPlans.Growth, SubscriptionPlans.Business];

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/platform/auth/login", Login).AllowAnonymous().RequireRateLimiting("Auth").AddEndpointFilter<ValidationFilter>();
        var group = app.MapGroup("/api/platform").RequireAuthorization("PlatformOwnerOnly");
        group.MapGet("/tenants", ListTenants);
        group.MapGet("/tenants/{id:guid}", GetTenant);
        group.MapPut("/tenants/{id:guid}/subscription", UpdateSubscription).AddEndpointFilter<ValidationFilter>();
        group.MapGet("/audit", GetAudit);
    }

    static async Task<IResult> Login(PlatformLoginRequest request, HttpContext http, AppDbContext db, IPasswordHasher<PlatformUser> hasher, PlatformTokenService tokens, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.PlatformUsers.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null || !user.IsActive || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return Results.Unauthorized();
        user.LastLoginAt = DateTimeOffset.UtcNow;
        db.PlatformAuditEvents.Add(Audit(user.Id, null, "platform.login", "Platform owner signed in.", http));
        await db.SaveChangesAsync(ct);
        return Results.Ok(tokens.Create(user));
    }

    static async Task<IResult> ListTenants(AppDbContext db, CancellationToken ct)
    {
        var activeUsers = await db.Users.IgnoreQueryFilters().Where(x => x.IsActive).GroupBy(x => x.TenantId).Select(x => new { TenantId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);
        var tenants = await db.Tenants.AsNoTracking().OrderBy(x => x.BusinessName).ToListAsync(ct);
        return Results.Ok(tenants.Select(x => ToDto(x, activeUsers.GetValueOrDefault(x.Id))).ToList());
    }

    static async Task<IResult> GetTenant(Guid id, AppDbContext db, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (tenant is null) return Results.NotFound();
        var activeUsers = await db.Users.IgnoreQueryFilters().CountAsync(x => x.TenantId == id && x.IsActive, ct);
        return Results.Ok(ToDto(tenant, activeUsers));
    }

    static async Task<IResult> UpdateSubscription(Guid id, PlatformSubscriptionUpdateRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var plan = Plans.SingleOrDefault(x => x.Equals(request.Plan, StringComparison.OrdinalIgnoreCase));
        var status = SubscriptionStatuses.All.SingleOrDefault(x => x.Equals(request.Status, StringComparison.OrdinalIgnoreCase));
        if (plan is null || status is null) return Results.BadRequest(new { message = "Select a valid subscription plan and status." });
        if (status == SubscriptionStatuses.Trialing && request.TrialEndsAt is null) return Results.BadRequest(new { message = "A trial end date is required for trialing subscriptions." });
        if (status == SubscriptionStatuses.PastDue && request.GraceEndsAt is null) return Results.BadRequest(new { message = "A grace end date is required for past-due subscriptions." });

        var actorId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({id.ToString()}))", ct);
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (tenant is null) return Results.NotFound();

        var before = $"{tenant.SubscriptionPlan}/{tenant.SubscriptionStatus}, limit {tenant.UserLimit}";
        tenant.SubscriptionPlan = plan;
        tenant.SubscriptionStatus = status;
        tenant.UserLimit = request.UserLimit;
        tenant.TrialEndsAt = request.TrialEndsAt;
        tenant.SubscriptionEndsAt = request.SubscriptionEndsAt;
        tenant.GraceEndsAt = request.GraceEndsAt;
        tenant.CancellationRequestedAt = status == SubscriptionStatuses.Cancelled ? DateTimeOffset.UtcNow : null;
        var after = $"{tenant.SubscriptionPlan}/{tenant.SubscriptionStatus}, limit {tenant.UserLimit}";
        db.PlatformAuditEvents.Add(Audit(actorId, tenant.Id, "tenant.subscription.updated", $"Changed subscription from {before} to {after}. Reason: {request.Reason.Trim()}", http));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var activeUsers = await db.Users.IgnoreQueryFilters().CountAsync(x => x.TenantId == id && x.IsActive, ct);
        return Results.Ok(ToDto(tenant, activeUsers));
    }

    static async Task<IResult> GetAudit(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.PlatformAuditEvents.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(200).Select(x => new PlatformAuditEventDto(x.Id, x.PlatformUserId, x.TargetTenantId, x.Action, x.Description, x.CorrelationId, x.IpAddress, x.CreatedAt)).ToListAsync(ct);
        return Results.Ok(rows);
    }

    static PlatformTenantDto ToDto(Tenant tenant, int activeUsers) => new(tenant.Id, tenant.BusinessName, tenant.Email, tenant.SubscriptionPlan, tenant.SubscriptionStatus, tenant.UserLimit, activeUsers, tenant.TrialEndsAt, tenant.SubscriptionEndsAt, tenant.GraceEndsAt, tenant.CreatedAt);

    static PlatformAuditEvent Audit(Guid actorId, Guid? tenantId, string action, string description, HttpContext http) => new()
    {
        PlatformUserId = actorId,
        TargetTenantId = tenantId,
        Action = action,
        Description = description,
        CorrelationId = http.TraceIdentifier,
        IpAddress = http.Connection.RemoteIpAddress?.ToString()
    };
}
