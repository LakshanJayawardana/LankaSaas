using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608120003_LoginActivity")]
public sealed class LoginActivity : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "LoginCount" bigint NOT NULL DEFAULT 0;
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "LastLoginAt" timestamptz NULL;
CREATE TABLE IF NOT EXISTS "LoginEvents" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "UserId" uuid NOT NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_LoginEvents_TenantId_CreatedAt" ON "LoginEvents" ("TenantId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_LoginEvents_UserId" ON "LoginEvents" ("UserId");
""");

    protected override void Down(MigrationBuilder m) => m.Sql("""
DROP TABLE IF EXISTS "LoginEvents";
ALTER TABLE "Users" DROP COLUMN IF EXISTS "LastLoginAt";
ALTER TABLE "Users" DROP COLUMN IF EXISTS "LoginCount";
""");
}
