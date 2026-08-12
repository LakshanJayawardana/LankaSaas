using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608120006_BillingManagement")]
public sealed class BillingManagement : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "PayHereSubscriptionId" text NULL;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "CancellationRequestedAt" timestamptz NULL;
""");
    protected override void Down(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "CancellationRequestedAt";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "PayHereSubscriptionId";
""");
}
