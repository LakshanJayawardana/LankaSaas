using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608120005_PayHerePayments")]
public sealed class PayHerePayments : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
CREATE TABLE IF NOT EXISTS "PaymentOrders" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "OrderId" text NOT NULL,
  "Plan" text NOT NULL, "Amount" numeric(18,2) NOT NULL, "Currency" text NOT NULL,
  "Status" text NOT NULL, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PaymentOrders_OrderId" ON "PaymentOrders" ("OrderId");
CREATE TABLE IF NOT EXISTS "PaymentTransactions" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "PaymentOrderId" uuid NOT NULL,
  "ProviderPaymentId" text NOT NULL, "Amount" numeric(18,2) NOT NULL,
  "Currency" text NOT NULL, "StatusCode" text NOT NULL, "PaymentMethod" text NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PaymentTransactions_ProviderPaymentId" ON "PaymentTransactions" ("ProviderPaymentId");
CREATE INDEX IF NOT EXISTS "IX_PaymentTransactions_PaymentOrderId" ON "PaymentTransactions" ("PaymentOrderId");
""");
    protected override void Down(MigrationBuilder m)=>m.Sql("""
DROP TABLE IF EXISTS "PaymentTransactions";
DROP TABLE IF EXISTS "PaymentOrders";
""");
}
