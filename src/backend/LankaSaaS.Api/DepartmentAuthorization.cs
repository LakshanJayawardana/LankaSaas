using System.Security.Claims;
using LankaSaaS.Domain;
using LankaSaaS.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

public sealed record PermissionRequirement(string Permission):IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(AppDbContext db):AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,PermissionRequirement requirement)
    {
        if(context.User.IsInRole(Roles.Admin)){context.Succeed(requirement);return;}
        if(requirement.Permission.StartsWith("administration.",StringComparison.Ordinal))return;
        if(!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier),out var userId))return;
        var grants=await (from membership in db.UserDepartments.AsNoTracking()
                          join permission in db.DepartmentPermissions.AsNoTracking() on membership.DepartmentId equals permission.DepartmentId
                          join department in db.Departments.AsNoTracking() on membership.DepartmentId equals department.Id
                          where membership.UserId==userId&&department.IsActive&&permission.PermissionCode==requirement.Permission
                          select new{membership.AccessLevel,permission.MinimumAccessLevel}).ToListAsync();
        if(grants.Any(x=>Rank(x.AccessLevel)>=Rank(x.MinimumAccessLevel)))context.Succeed(requirement);
    }

    static int Rank(string level)=>level switch{DepartmentAccessLevels.Viewer=>1,DepartmentAccessLevels.Member=>2,DepartmentAccessLevels.Manager=>3,_=>0};
}

public static class DepartmentDefaults
{
    public static List<Department> AddForTenant(AppDbContext db,Guid tenantId,Guid administratorId)
    {
        var definitions=new[]
        {
            Template("ADMINISTRATION","Administration",[(Permissions.AdministrationUsers,DepartmentAccessLevels.Manager),(Permissions.AdministrationSettings,DepartmentAccessLevels.Manager),(Permissions.AdministrationBilling,DepartmentAccessLevels.Manager),(Permissions.AdministrationAudit,DepartmentAccessLevels.Manager)]),
            Template("EVENTS","Event Management",[(Permissions.EventsView,DepartmentAccessLevels.Viewer),(Permissions.EventsManage,DepartmentAccessLevels.Member),(Permissions.EventsChangeStatus,DepartmentAccessLevels.Manager),(Permissions.ContactsView,DepartmentAccessLevels.Viewer),(Permissions.ContactsManage,DepartmentAccessLevels.Member),(Permissions.StaffingView,DepartmentAccessLevels.Viewer),(Permissions.StaffingManage,DepartmentAccessLevels.Member),(Permissions.AttendanceSelf,DepartmentAccessLevels.Viewer),(Permissions.LogisticsView,DepartmentAccessLevels.Viewer),(Permissions.FinanceView,DepartmentAccessLevels.Viewer),(Permissions.FinanceQuotations,DepartmentAccessLevels.Member)]),
            Template("LOGISTICS","Logistics",[(Permissions.EventsView,DepartmentAccessLevels.Viewer),(Permissions.AttendanceSelf,DepartmentAccessLevels.Viewer),(Permissions.LogisticsView,DepartmentAccessLevels.Viewer),(Permissions.LogisticsOperate,DepartmentAccessLevels.Member),(Permissions.LogisticsManage,DepartmentAccessLevels.Manager)]),
            Template("FINANCE","Accounting & Finance",[(Permissions.EventsView,DepartmentAccessLevels.Viewer),(Permissions.AttendanceSelf,DepartmentAccessLevels.Viewer),(Permissions.FinanceView,DepartmentAccessLevels.Viewer),(Permissions.AccountingView,DepartmentAccessLevels.Viewer),(Permissions.PurchasingView,DepartmentAccessLevels.Viewer),(Permissions.FinanceQuotations,DepartmentAccessLevels.Member),(Permissions.FinancePayments,DepartmentAccessLevels.Member),(Permissions.AccountingPostJournals,DepartmentAccessLevels.Manager),(Permissions.FinanceManage,DepartmentAccessLevels.Manager)]),
            Template("PURCHASING","Purchasing",[(Permissions.EventsView,DepartmentAccessLevels.Viewer),(Permissions.AttendanceSelf,DepartmentAccessLevels.Viewer),(Permissions.PurchasingView,DepartmentAccessLevels.Viewer),(Permissions.PurchasingOperate,DepartmentAccessLevels.Member),(Permissions.PurchasingManage,DepartmentAccessLevels.Manager),(Permissions.LogisticsView,DepartmentAccessLevels.Viewer)]),
            Template("PEOPLE","People & Attendance",[(Permissions.EventsView,DepartmentAccessLevels.Viewer),(Permissions.StaffingView,DepartmentAccessLevels.Viewer),(Permissions.AttendanceSelf,DepartmentAccessLevels.Viewer),(Permissions.StaffingManage,DepartmentAccessLevels.Member),(Permissions.AttendanceOverride,DepartmentAccessLevels.Manager)]),
            Template("GENERAL","General Staff",[(Permissions.EventsView,DepartmentAccessLevels.Viewer),(Permissions.StaffingView,DepartmentAccessLevels.Viewer),(Permissions.AttendanceSelf,DepartmentAccessLevels.Viewer)])
        };
        var departments=definitions.Select(x=>new Department{TenantId=tenantId,Name=x.Name,Code=x.Code,IsSystem=true}).ToList();
        db.Departments.AddRange(departments);
        foreach(var pair in definitions.Zip(departments))db.DepartmentPermissions.AddRange(pair.First.Permissions.Select(p=>new DepartmentPermission{TenantId=tenantId,DepartmentId=pair.Second.Id,PermissionCode=p.Permission,MinimumAccessLevel=p.Level}));
        var adminDepartment=departments.Single(x=>x.Code=="ADMINISTRATION");
        db.UserDepartments.Add(new UserDepartment{TenantId=tenantId,UserId=administratorId,DepartmentId=adminDepartment.Id,AccessLevel=DepartmentAccessLevels.Manager,IsPrimary=true});
        return departments;
    }

    static (string Code,string Name,(string Permission,string Level)[] Permissions) Template(string code,string name,(string Permission,string Level)[] permissions)=>(code,name,permissions);
}
