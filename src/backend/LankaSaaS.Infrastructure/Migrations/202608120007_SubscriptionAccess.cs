using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608120007_SubscriptionAccess")]
public sealed class SubscriptionAccess : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "GraceEndsAt" timestamptz NULL;
""");
    protected override void Down(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "GraceEndsAt";
""");
}
