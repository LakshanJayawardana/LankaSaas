using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LankaSaaS.Infrastructure.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("202608120008_Events")]
public sealed class Events : Migration
{
 protected override void Up(MigrationBuilder m)=>m.Sql("""
CREATE TABLE IF NOT EXISTS "Events" (
 "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"CustomerId" uuid NOT NULL,"CustomerName" text NOT NULL,
 "Name" text NOT NULL,"Venue" text NOT NULL,"StartsAt" timestamptz NOT NULL,"EndsAt" timestamptz NOT NULL,
 "Status" text NOT NULL,"BudgetedRevenue" numeric(18,2) NOT NULL,"BudgetedCost" numeric(18,2) NOT NULL,
 "Notes" text NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_Events_TenantId_StartsAt" ON "Events" ("TenantId","StartsAt");
ALTER TABLE "Expenses" ADD COLUMN IF NOT EXISTS "EventId" uuid NULL;
CREATE INDEX IF NOT EXISTS "IX_Expenses_EventId" ON "Expenses" ("EventId");
""");
 protected override void Down(MigrationBuilder m)=>m.Sql("""
DROP INDEX IF EXISTS "IX_Expenses_EventId";
ALTER TABLE "Expenses" DROP COLUMN IF EXISTS "EventId";
DROP TABLE IF EXISTS "Events";
""");
}
