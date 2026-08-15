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
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/platform/auth/login", Login).AllowAnonymous().RequireRateLimiting("PlatformAuth").AddEndpointFilter<ValidationFilter>();
        var group = app.MapGroup("/api/platform").RequireAuthorization("PlatformOwnerOnly");
        group.MapGet("/tenants", ListTenants);
        group.MapGet("/tenants/{id:guid}", GetTenant);
        group.MapPut("/tenants/{id:guid}/subscription", UpdateSubscription).AddEndpointFilter<ValidationFilter>();
        group.MapGet("/audit", GetAudit);
        group.MapGet("/owners", GetOwners);
        group.MapPost("/owners", CreateOwner).AddEndpointFilter<ValidationFilter>();
        group.MapPut("/owners/{id:guid}/access", UpdateOwnerAccess).AddEndpointFilter<ValidationFilter>();
        group.MapPost("/auth/change-password", ChangePassword).AddEndpointFilter<ValidationFilter>();
        group.MapPut("/tenants/{id:guid}/archive", ArchiveTenant).AddEndpointFilter<ValidationFilter>();
        group.MapPost("/test-tenants/archive", ArchiveTestTenants).AddEndpointFilter<ValidationFilter>();
        group.MapGet("/plans", GetPlans);
        group.MapPut("/plans/{code}", UpdatePlan).AddEndpointFilter<ValidationFilter>();
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
        var plan = request.Plan.Equals(SubscriptionPlans.Trial, StringComparison.OrdinalIgnoreCase)
            ? SubscriptionPlans.Trial
            : (await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Code.ToLower() == request.Plan.Trim().ToLower(), ct))?.Code;
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

    static async Task<IResult> GetPlans(AppDbContext db, CancellationToken ct)
    {
        var plans = await db.SubscriptionPlans.AsNoTracking().OrderBy(x => x.MonthlyPriceLkr)
            .Select(x => new PlatformSubscriptionPlanDto(x.Code, x.Name, x.MonthlyPriceLkr, x.UserLimit, x.Description, x.IsActive, x.UpdatedAt)).ToListAsync(ct);
        return Results.Ok(plans);
    }

    static async Task<IResult> UpdatePlan(string code, UpdatePlatformSubscriptionPlanRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(x => x.Code.ToLower() == code.Trim().ToLower(), ct);
        if (plan is null) return Results.NotFound(new { message = "Subscription plan was not found." });
        var before = $"{plan.Name}: LKR {plan.MonthlyPriceLkr:0.00}, {plan.UserLimit} users, {(plan.IsActive ? "active" : "inactive")}";
        plan.Name = request.Name.Trim();
        plan.MonthlyPriceLkr = request.MonthlyPriceLkr;
        plan.UserLimit = request.UserLimit;
        plan.Description = request.Description.Trim();
        plan.IsActive = request.IsActive;
        var after = $"{plan.Name}: LKR {plan.MonthlyPriceLkr:0.00}, {plan.UserLimit} users, {(plan.IsActive ? "active" : "inactive")}";
        var actorId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.PlatformAuditEvents.Add(Audit(actorId, null, "subscription.plan.updated", $"Changed {plan.Code} from [{before}] to [{after}]. Reason: {request.Reason.Trim()}", http));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new PlatformSubscriptionPlanDto(plan.Code, plan.Name, plan.MonthlyPriceLkr, plan.UserLimit, plan.Description, plan.IsActive, plan.UpdatedAt));
    }

    static async Task<IResult> GetAudit(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.PlatformAuditEvents.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(200).Select(x => new PlatformAuditEventDto(x.Id, x.PlatformUserId, x.TargetTenantId, x.Action, x.Description, x.CorrelationId, x.IpAddress, x.CreatedAt)).ToListAsync(ct);
        return Results.Ok(rows);
    }

    static async Task<IResult> GetOwners(AppDbContext db, CancellationToken ct)
    {
        var owners = await db.PlatformUsers.AsNoTracking().OrderBy(x => x.Email).Select(x => new PlatformUserDto(x.Id, x.Email, x.Role, x.IsActive, x.LastLoginAt, x.CreatedAt)).ToListAsync(ct);
        return Results.Ok(owners);
    }

    static async Task<IResult> CreateOwner(CreatePlatformUserRequest request, HttpContext http, AppDbContext db, IPasswordHasher<PlatformUser> hasher, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.PlatformUsers.AnyAsync(x => x.Email == email, ct)) return Results.Conflict(new { message = "A platform owner with this email already exists." });
        var owner = new PlatformUser { Email = email, PasswordHash = string.Empty };
        owner.PasswordHash = hasher.HashPassword(owner, request.Password);
        db.PlatformUsers.Add(owner);
        var actorId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.PlatformAuditEvents.Add(Audit(actorId, null, "platform.owner.created", $"Created platform owner {email}.", http));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/platform/owners/{owner.Id}", ToDto(owner));
    }

    static async Task<IResult> UpdateOwnerAccess(Guid id, UpdatePlatformUserAccessRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(78123901)", ct);
        var owner = await db.PlatformUsers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (owner is null) return Results.NotFound();
        if (!request.IsActive && owner.IsActive && await db.PlatformUsers.CountAsync(x => x.IsActive, ct) <= 1) return Results.Conflict(new { message = "The final active platform owner cannot be deactivated." });
        if (owner.IsActive == request.IsActive) return Results.Ok(ToDto(owner));
        owner.IsActive = request.IsActive;
        owner.AccessVersion++;
        var actorId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var action = request.IsActive ? "platform.owner.activated" : "platform.owner.deactivated";
        db.PlatformAuditEvents.Add(Audit(actorId, null, action, $"{(request.IsActive ? "Activated" : "Deactivated")} platform owner {owner.Email}. Reason: {request.Reason.Trim()}", http));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(ToDto(owner));
    }

    static async Task<IResult> ChangePassword(ChangePlatformPasswordRequest request, HttpContext http, AppDbContext db, IPasswordHasher<PlatformUser> hasher, CancellationToken ct)
    {
        var actorId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var owner = await db.PlatformUsers.SingleAsync(x => x.Id == actorId, ct);
        if (hasher.VerifyHashedPassword(owner, owner.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed) return Results.BadRequest(new { message = "The current password is incorrect." });
        if (request.CurrentPassword == request.NewPassword) return Results.BadRequest(new { message = "Choose a new password that is different from the current password." });
        owner.PasswordHash = hasher.HashPassword(owner, request.NewPassword);
        owner.AccessVersion++;
        db.PlatformAuditEvents.Add(Audit(actorId, null, "platform.owner.password_changed", $"Platform owner {owner.Email} changed their password and revoked existing sessions.", http));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    static async Task<IResult> ArchiveTenant(Guid id, ArchiveTenantRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var actorId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({id.ToString()}))", ct);
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (tenant is null) return Results.NotFound();
        if (tenant.IsArchived == request.IsArchived) return Results.Ok(ToDto(tenant, await db.Users.IgnoreQueryFilters().CountAsync(x=>x.TenantId==id&&x.IsActive,ct)));
        tenant.IsArchived = request.IsArchived;
        tenant.ArchivedAt = request.IsArchived ? DateTimeOffset.UtcNow : null;
        tenant.ArchivedReason = request.IsArchived ? request.Reason.Trim() : null;
        if (request.IsArchived) tenant.SubscriptionStatus = SubscriptionStatuses.Suspended;
        await db.Users.IgnoreQueryFilters().Where(x=>x.TenantId==id).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.AccessVersion,u=>u.AccessVersion+1),ct);
        var action=request.IsArchived?"tenant.archived":"tenant.restored";
        db.PlatformAuditEvents.Add(Audit(actorId,id,action,$"{(request.IsArchived?"Archived":"Restored")} tenant {tenant.BusinessName}. Reason: {request.Reason.Trim()}",http));
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        var activeUsers=await db.Users.IgnoreQueryFilters().CountAsync(x=>x.TenantId==id&&x.IsActive,ct);
        return Results.Ok(ToDto(tenant,activeUsers));
    }

    static async Task<IResult> ArchiveTestTenants(ArchiveTestTenantsRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        if(request.Confirmation!="ARCHIVE TEST TENANTS")return Results.BadRequest(new{message="Enter ARCHIVE TEST TENANTS to confirm this bulk action."});
        var ids=await db.Tenants.Where(x=>x.IsTestTenant&&!x.IsArchived).Select(x=>x.Id).ToListAsync(ct);
        if(ids.Count==0)return Results.Ok(new{archivedCount=0});
        var now=DateTimeOffset.UtcNow;await using var transaction=await db.Database.BeginTransactionAsync(ct);await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(78123902)",ct);
        await db.Tenants.Where(x=>ids.Contains(x.Id)).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.IsArchived,true).SetProperty(t=>t.ArchivedAt,now).SetProperty(t=>t.ArchivedReason,request.Reason.Trim()).SetProperty(t=>t.SubscriptionStatus,SubscriptionStatuses.Suspended),ct);
        await db.Users.IgnoreQueryFilters().Where(x=>ids.Contains(x.TenantId)).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.AccessVersion,u=>u.AccessVersion+1),ct);
        var actorId=Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);db.PlatformAuditEvents.Add(Audit(actorId,null,"test_tenants.archived",$"Archived {ids.Count} explicitly marked automated-test tenants. Reason: {request.Reason.Trim()}",http));await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return Results.Ok(new{archivedCount=ids.Count});
    }

    static PlatformTenantDto ToDto(Tenant tenant, int activeUsers) => new(tenant.Id, tenant.BusinessName, tenant.Email, tenant.SubscriptionPlan, tenant.SubscriptionStatus, tenant.UserLimit, activeUsers, tenant.TrialEndsAt, tenant.SubscriptionEndsAt, tenant.GraceEndsAt, tenant.IsTestTenant, tenant.IsArchived, tenant.ArchivedAt, tenant.ArchivedReason, tenant.CreatedAt);
    static PlatformUserDto ToDto(PlatformUser owner) => new(owner.Id, owner.Email, owner.Role, owner.IsActive, owner.LastLoginAt, owner.CreatedAt);

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
