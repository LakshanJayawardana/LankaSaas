using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608120004_Subscriptions")]
public sealed class Subscriptions : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "SubscriptionPlan" text NOT NULL DEFAULT 'Trial';
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "SubscriptionStatus" text NOT NULL DEFAULT 'Trialing';
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "UserLimit" integer NOT NULL DEFAULT 3;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "TrialEndsAt" timestamptz NULL;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "SubscriptionEndsAt" timestamptz NULL;
UPDATE "Tenants" SET "TrialEndsAt"=CURRENT_TIMESTAMP + INTERVAL '14 days' WHERE "SubscriptionPlan"='Trial' AND "TrialEndsAt" IS NULL;
""");

    protected override void Down(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "SubscriptionEndsAt";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "TrialEndsAt";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "UserLimit";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "SubscriptionStatus";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "SubscriptionPlan";
""");
}
