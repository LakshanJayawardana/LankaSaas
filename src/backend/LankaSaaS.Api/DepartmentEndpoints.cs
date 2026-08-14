using LankaSaaS.Application;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class DepartmentEndpoints
{
    public static void Map(WebApplication app)
    {
        var g=app.MapGroup("/api/departments").RequireAuthorization("AdminOnly").AddEndpointFilter<ValidationFilter>();
        g.MapGet("/",List);
        g.MapGet("/permissions",()=>Results.Ok(Permissions.All));
        g.MapGet("/access-history",History);
        g.MapPost("/",Create);
        g.MapPut("/{id:guid}",Update);
        g.MapDelete("/{id:guid}",Delete);
        app.MapGet("/api/departments/my-access",MyAccess).RequireAuthorization();
        app.MapPut("/api/users/{userId:guid}/departments",AssignUser).RequireAuthorization("AdminOnly").AddEndpointFilter<ValidationFilter>();
    }

    static async Task<IResult> List(AppDbContext db,CancellationToken ct)
    {
        var departments=await db.Departments.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
        var permissions=await db.DepartmentPermissions.AsNoTracking().ToListAsync(ct);
        return Results.Ok(departments.Select(x=>Dto(x,permissions.Where(p=>p.DepartmentId==x.Id))).ToList());
    }

    static async Task<IResult> History(AppDbContext db,CancellationToken ct)
    {
        var events=await db.AuditEvents.AsNoTracking().Where(x=>x.Description!=null&&x.Path.StartsWith("/api/departments/access-change")).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(ct);
        var userIds=events.Select(x=>x.UserId).Distinct().ToList();var users=await db.Users.AsNoTracking().Where(x=>userIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.FirstName+" "+x.LastName,ct);
        return Results.Ok(events.Select(x=>new AccessHistoryDto(x.Id,x.UserId,users.GetValueOrDefault(x.UserId,"Unknown user"),x.Description!,x.CreatedAt)).ToList());
    }

    static async Task<IResult> MyAccess(HttpContext http,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        if(http.User.IsInRole(Roles.Admin))return Results.Ok(new{isAdministrator=true,permissions=Permissions.All});
        var grants=await (from membership in db.UserDepartments.AsNoTracking()
                          join permission in db.DepartmentPermissions.AsNoTracking() on membership.DepartmentId equals permission.DepartmentId
                          join department in db.Departments.AsNoTracking() on membership.DepartmentId equals department.Id
                          where membership.UserId==tenant.UserId&&department.IsActive
                          select new{membership.AccessLevel,permission.PermissionCode,permission.MinimumAccessLevel}).ToListAsync(ct);
        var allowed=grants.Where(x=>!x.PermissionCode.StartsWith("administration.",StringComparison.Ordinal)&&Rank(x.AccessLevel)>=Rank(x.MinimumAccessLevel)).Select(x=>x.PermissionCode).Distinct().OrderBy(x=>x).ToList();
        return Results.Ok(new{isAdministrator=false,permissions=allowed});
    }

    static async Task<IResult> Create(DepartmentRequest r,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        var validation=Validate(r);if(validation is not null)return validation;
        var code=r.Code.Trim().ToUpperInvariant();if(await db.Departments.AnyAsync(x=>x.Code==code,ct))return Results.Conflict(new{message="A department with this code already exists."});
        var department=new Department{TenantId=tenant.TenantId,Name=r.Name.Trim(),Code=code,IsActive=r.IsActive};db.Departments.Add(department);db.DepartmentPermissions.AddRange(r.Permissions.Select(x=>new DepartmentPermission{TenantId=tenant.TenantId,DepartmentId=department.Id,PermissionCode=x.PermissionCode,MinimumAccessLevel=x.MinimumAccessLevel}));AddAccessAudit(db,tenant,$"Created department {department.Name} ({department.Code}).");await db.SaveChangesAsync(ct);return Results.Created($"/api/departments/{department.Id}",Dto(department,db.DepartmentPermissions.Local.Where(x=>x.DepartmentId==department.Id)));
    }

    static async Task<IResult> Update(Guid id,DepartmentRequest r,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        var validation=Validate(r);if(validation is not null)return validation;
        var department=await db.Departments.SingleOrDefaultAsync(x=>x.Id==id,ct);if(department is null)return Results.NotFound();var code=r.Code.Trim().ToUpperInvariant();
        if(department.IsSystem&&code!=department.Code)return Results.Conflict(new{message="System department codes cannot be changed."});if(await db.Departments.AnyAsync(x=>x.Id!=id&&x.Code==code,ct))return Results.Conflict(new{message="A department with this code already exists."});
        var old=await db.DepartmentPermissions.Where(x=>x.DepartmentId==id).ToListAsync(ct);db.DepartmentPermissions.RemoveRange(old);department.Name=r.Name.Trim();department.Code=code;department.IsActive=r.IsActive;db.DepartmentPermissions.AddRange(r.Permissions.Select(x=>new DepartmentPermission{DepartmentId=id,PermissionCode=x.PermissionCode,MinimumAccessLevel=x.MinimumAccessLevel}));AddAccessAudit(db,tenant,$"Updated department {department.Name}: {r.Permissions.Count} permissions, {(department.IsActive?"active":"inactive")}.");await db.SaveChangesAsync(ct);return Results.Ok(Dto(department,await db.DepartmentPermissions.AsNoTracking().Where(x=>x.DepartmentId==id).ToListAsync(ct)));
    }

    static async Task<IResult> Delete(Guid id,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        var department=await db.Departments.SingleOrDefaultAsync(x=>x.Id==id,ct);if(department is null)return Results.NotFound();if(department.IsSystem)return Results.Conflict(new{message="System departments cannot be deleted. Deactivate them instead."});if(await db.UserDepartments.AnyAsync(x=>x.DepartmentId==id,ct))return Results.Conflict(new{message="Reassign department members before deleting this department."});db.Departments.Remove(department);AddAccessAudit(db,tenant,$"Deleted department {department.Name} ({department.Code}).");await db.SaveChangesAsync(ct);return Results.NoContent();
    }

    static async Task<IResult> AssignUser(Guid userId,UpdateUserDepartmentsRequest r,AppDbContext db,ITenantContext tenant,CancellationToken ct)
    {
        if(r.Departments is null||r.Departments.Count==0||r.Departments.Select(x=>x.DepartmentId).Distinct().Count()!=r.Departments.Count||r.Departments.Count(x=>x.IsPrimary)!=1||r.Departments.Any(x=>!ValidLevel(x.AccessLevel)))return Results.BadRequest(new{message="Assign at least one unique department, exactly one primary department, and a valid access level."});
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==userId,ct);if(user is null)return Results.NotFound();var ids=r.Departments.Select(x=>x.DepartmentId).ToList();if(await db.Departments.CountAsync(x=>ids.Contains(x.Id)&&x.IsActive,ct)!=ids.Count)return Results.BadRequest(new{message="One or more departments were not found or are inactive."});
        await using var tx=await db.Database.BeginTransactionAsync(ct);var old=await db.UserDepartments.Where(x=>x.UserId==userId).ToListAsync(ct);db.UserDepartments.RemoveRange(old);await db.SaveChangesAsync(ct);db.UserDepartments.AddRange(r.Departments.Select(x=>new UserDepartment{UserId=userId,DepartmentId=x.DepartmentId,AccessLevel=NormalizeLevel(x.AccessLevel),IsPrimary=x.IsPrimary}));user.AccessVersion++;await db.RefreshTokens.Where(x=>x.UserId==userId).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.RevokedAt,DateTimeOffset.UtcNow),ct);AddAccessAudit(db,tenant,$"Updated department access for {user.FirstName} {user.LastName}: {r.Departments.Count} department assignment(s).");await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.NoContent();
    }

    static IResult? Validate(DepartmentRequest r)
    {
        if(r.Permissions is null||r.Permissions.Count==0)return Results.BadRequest(new{message="Assign at least one permission to the department."});if(r.Permissions.Select(x=>x.PermissionCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=r.Permissions.Count)return Results.BadRequest(new{message="Department permissions must be unique."});
        if(r.Permissions.Any(x=>!Permissions.All.Contains(x.PermissionCode,StringComparer.Ordinal)||!ValidLevel(x.MinimumAccessLevel)))return Results.BadRequest(new{message="One or more permissions or minimum access levels are invalid."});return null;
    }
    static bool ValidLevel(string level)=>DepartmentAccessLevels.All.Contains(level,StringComparer.OrdinalIgnoreCase);
    static string NormalizeLevel(string level)=>DepartmentAccessLevels.All.Single(x=>x.Equals(level,StringComparison.OrdinalIgnoreCase));
    static int Rank(string level)=>level switch{DepartmentAccessLevels.Viewer=>1,DepartmentAccessLevels.Member=>2,DepartmentAccessLevels.Manager=>3,_=>0};
    static void AddAccessAudit(AppDbContext db,ITenantContext tenant,string description)=>db.AuditEvents.Add(new AuditEvent{TenantId=tenant.TenantId,UserId=tenant.UserId,Method="ACCESS",Path="/api/departments/access-change",StatusCode=200,CorrelationId=Guid.NewGuid().ToString("N"),Description=description});
    static DepartmentDto Dto(Department x,IEnumerable<DepartmentPermission> permissions)=>new(x.Id,x.Name,x.Code,x.IsSystem,x.IsActive,permissions.OrderBy(p=>p.PermissionCode).Select(p=>new DepartmentPermissionDto(p.PermissionCode,p.MinimumAccessLevel)).ToList());
}
