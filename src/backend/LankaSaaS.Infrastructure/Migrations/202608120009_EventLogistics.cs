using Microsoft.EntityFrameworkCore.Infrastructure;using Microsoft.EntityFrameworkCore.Migrations;
namespace LankaSaaS.Infrastructure.Migrations;
[DbContext(typeof(AppDbContext))][Migration("202608120009_EventLogistics")]
public sealed class EventLogistics:Migration
{
 protected override void Up(MigrationBuilder m)=>m.Sql("""
CREATE TABLE IF NOT EXISTS "LogisticsResources" ("Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"Name" text NOT NULL,"Type" text NOT NULL,"Identifier" text NULL,"TotalQuantity" integer NOT NULL,"Status" text NOT NULL,"Notes" text NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_LogisticsResources_TenantId_Name" ON "LogisticsResources" ("TenantId","Name");
CREATE TABLE IF NOT EXISTS "EventResourceAllocations" ("Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"EventId" uuid NOT NULL,"ResourceId" uuid NOT NULL,"ResourceName" text NOT NULL,"Quantity" integer NOT NULL,"Status" text NOT NULL,"ReturnedQuantity" integer NOT NULL,"DamagedQuantity" integer NOT NULL,"MissingQuantity" integer NOT NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_EventResourceAllocations_EventId_ResourceId" ON "EventResourceAllocations" ("EventId","ResourceId");
CREATE TABLE IF NOT EXISTS "EventChecklistItems" ("Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"EventId" uuid NOT NULL,"Description" text NOT NULL,"IsCompleted" boolean NOT NULL,"CompletedByUserId" uuid NULL,"CompletedAt" timestamptz NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_EventChecklistItems_EventId" ON "EventChecklistItems" ("EventId");
""");
 protected override void Down(MigrationBuilder m)=>m.Sql("""DROP TABLE IF EXISTS "EventChecklistItems";DROP TABLE IF EXISTS "EventResourceAllocations";DROP TABLE IF EXISTS "LogisticsResources";""");
}
