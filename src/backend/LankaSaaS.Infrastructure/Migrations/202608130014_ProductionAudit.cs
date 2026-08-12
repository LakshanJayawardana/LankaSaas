using Microsoft.EntityFrameworkCore.Infrastructure;using Microsoft.EntityFrameworkCore.Migrations;namespace LankaSaaS.Infrastructure.Migrations;
[DbContext(typeof(AppDbContext))][Migration("202608130014_ProductionAudit")]public sealed class ProductionAudit:Migration
{
 protected override void Up(MigrationBuilder m)=>m.Sql("""
CREATE TABLE IF NOT EXISTS "AuditEvents" ("Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL,"UserId" uuid NOT NULL,"Method" text NOT NULL,"Path" text NOT NULL,"StatusCode" integer NOT NULL,"CorrelationId" text NOT NULL,"IpAddress" text NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NOT NULL);CREATE INDEX IF NOT EXISTS "IX_AuditEvents_TenantId_CreatedAt" ON "AuditEvents" ("TenantId","CreatedAt");
""");protected override void Down(MigrationBuilder m)=>m.Sql("""DROP TABLE IF EXISTS "AuditEvents";""");
}
