using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608140017_DepartmentAccessControl")]
public sealed class DepartmentAccessControl:Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
CREATE TABLE IF NOT EXISTS "Departments" (
    "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"Name" character varying(80) NOT NULL,"Code" character varying(30) NOT NULL,
    "IsSystem" boolean NOT NULL DEFAULT false,"IsActive" boolean NOT NULL DEFAULT true,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Departments_TenantId_Code" ON "Departments" ("TenantId","Code");

CREATE TABLE IF NOT EXISTS "DepartmentPermissions" (
    "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"DepartmentId" uuid NOT NULL,"PermissionCode" character varying(80) NOT NULL,
    "MinimumAccessLevel" character varying(20) NOT NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_DepartmentPermissions_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments"("Id") ON DELETE CASCADE);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_DepartmentPermissions_DepartmentId_PermissionCode" ON "DepartmentPermissions" ("DepartmentId","PermissionCode");

CREATE TABLE IF NOT EXISTS "UserDepartments" (
    "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"UserId" uuid NOT NULL,"DepartmentId" uuid NOT NULL,"AccessLevel" character varying(20) NOT NULL,
    "IsPrimary" boolean NOT NULL DEFAULT false,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_UserDepartments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserDepartments_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments"("Id") ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserDepartments_UserId_DepartmentId" ON "UserDepartments" ("UserId","DepartmentId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserDepartments_UserId_IsPrimary" ON "UserDepartments" ("UserId","IsPrimary") WHERE "IsPrimary" = TRUE;

INSERT INTO "Departments" ("Id","TenantId","Name","Code","IsSystem","IsActive","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),t."Id",d.name,d.code,true,true,NOW(),NOW() FROM "Tenants" t CROSS JOIN (VALUES
('Administration','ADMINISTRATION'),('Event Management','EVENTS'),('Logistics','LOGISTICS'),('Accounting & Finance','FINANCE'),('Purchasing','PURCHASING'),('People & Attendance','PEOPLE'),('General Staff','GENERAL')) d(name,code)
ON CONFLICT ("TenantId","Code") DO NOTHING;

INSERT INTO "DepartmentPermissions" ("Id","TenantId","DepartmentId","PermissionCode","MinimumAccessLevel","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),d."TenantId",d."Id",p.permission,p.level,NOW(),NOW() FROM "Departments" d JOIN (VALUES
('ADMINISTRATION','administration.users','Manager'),('ADMINISTRATION','administration.settings','Manager'),('ADMINISTRATION','administration.billing','Manager'),('ADMINISTRATION','administration.audit','Manager'),
('EVENTS','events.view','Viewer'),('EVENTS','events.manage','Member'),('EVENTS','events.change_status','Manager'),('EVENTS','contacts.view','Viewer'),('EVENTS','contacts.manage','Member'),('EVENTS','staffing.view','Viewer'),('EVENTS','staffing.manage','Member'),('EVENTS','attendance.self','Viewer'),('EVENTS','logistics.view','Viewer'),('EVENTS','finance.view','Viewer'),('EVENTS','finance.quotations','Member'),
('LOGISTICS','events.view','Viewer'),('LOGISTICS','attendance.self','Viewer'),('LOGISTICS','logistics.view','Viewer'),('LOGISTICS','logistics.operate','Member'),('LOGISTICS','logistics.manage','Manager'),
('FINANCE','events.view','Viewer'),('FINANCE','attendance.self','Viewer'),('FINANCE','finance.view','Viewer'),('FINANCE','accounting.view','Viewer'),('FINANCE','purchasing.view','Viewer'),('FINANCE','finance.quotations','Member'),('FINANCE','finance.payments','Member'),('FINANCE','accounting.post_journals','Manager'),('FINANCE','finance.manage','Manager'),
('PURCHASING','events.view','Viewer'),('PURCHASING','attendance.self','Viewer'),('PURCHASING','purchasing.view','Viewer'),('PURCHASING','purchasing.operate','Member'),('PURCHASING','purchasing.manage','Manager'),('PURCHASING','logistics.view','Viewer'),
('PEOPLE','events.view','Viewer'),('PEOPLE','staffing.view','Viewer'),('PEOPLE','attendance.self','Viewer'),('PEOPLE','staffing.manage','Member'),('PEOPLE','attendance.override','Manager'),
('GENERAL','events.view','Viewer'),('GENERAL','staffing.view','Viewer'),('GENERAL','attendance.self','Viewer')) p(code,permission,level) ON d."Code"=p.code
ON CONFLICT ("DepartmentId","PermissionCode") DO NOTHING;

INSERT INTO "UserDepartments" ("Id","TenantId","UserId","DepartmentId","AccessLevel","IsPrimary","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),u."TenantId",u."Id",d."Id",CASE WHEN u."Role"='Admin' THEN 'Manager' ELSE 'Viewer' END,true,NOW(),NOW()
FROM "Users" u JOIN "Departments" d ON d."TenantId"=u."TenantId" AND d."Code"=CASE WHEN u."Role"='Admin' THEN 'ADMINISTRATION' ELSE 'GENERAL' END
ON CONFLICT ("UserId","DepartmentId") DO NOTHING;
""");

    protected override void Down(MigrationBuilder m)=>m.Sql("""
DROP TABLE IF EXISTS "UserDepartments";
DROP TABLE IF EXISTS "DepartmentPermissions";
DROP TABLE IF EXISTS "Departments";
""");
}
