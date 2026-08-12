using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608120001_InitialSchema")]
public sealed class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE IF NOT EXISTS "Tenants" (
  "Id" uuid PRIMARY KEY, "Name" text NOT NULL, "BusinessName" text NOT NULL,
  "Email" text NOT NULL, "Phone" text NULL, "Address" text NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tenants_Email" ON "Tenants" ("Email");

CREATE TABLE IF NOT EXISTS "Users" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "FirstName" text NOT NULL,
  "LastName" text NOT NULL, "Email" text NOT NULL, "PasswordHash" text NOT NULL,
  "Role" text NOT NULL, "IsActive" boolean NOT NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");

CREATE TABLE IF NOT EXISTS "Customers" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "Name" text NOT NULL,
  "Phone" text NULL, "Email" text NULL, "Address" text NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);

CREATE TABLE IF NOT EXISTS "Products" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "Name" text NOT NULL,
  "SKU" text NOT NULL, "Description" text NULL, "SellingPrice" numeric(18,2) NOT NULL,
  "CostPrice" numeric(18,2) NOT NULL, "StockQuantity" integer NOT NULL,
  "IsActive" boolean NOT NULL, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_TenantId_SKU" ON "Products" ("TenantId", "SKU");

CREATE TABLE IF NOT EXISTS "Expenses" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" text NOT NULL,
  "Amount" numeric(18,2) NOT NULL, "ExpenseDate" date NOT NULL, "Category" text NOT NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);

CREATE TABLE IF NOT EXISTS "RefreshTokens" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "UserId" uuid NOT NULL,
  "TokenHash" text NOT NULL, "ExpiresAt" timestamptz NOT NULL, "RevokedAt" timestamptz NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);

CREATE TABLE IF NOT EXISTS "Invoices" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "CustomerId" uuid NOT NULL,
  "InvoiceNumber" text NOT NULL, "CustomerName" text NOT NULL, "IssueDate" date NOT NULL,
  "DueDate" date NOT NULL, "Status" integer NOT NULL, "Subtotal" numeric(18,2) NOT NULL,
  "DiscountTotal" numeric(18,2) NOT NULL, "TaxTotal" numeric(18,2) NOT NULL,
  "Total" numeric(18,2) NOT NULL, "Notes" text NULL,
  "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Invoices_TenantId_InvoiceNumber" ON "Invoices" ("TenantId", "InvoiceNumber");

CREATE TABLE IF NOT EXISTS "InvoiceItems" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "InvoiceId" uuid NOT NULL,
  "ProductId" uuid NULL, "Description" text NOT NULL, "Quantity" numeric(18,2) NOT NULL,
  "UnitPrice" numeric(18,2) NOT NULL, "Discount" numeric(18,2) NOT NULL,
  "TaxRate" numeric(18,2) NOT NULL, "LineSubtotal" numeric(18,2) NOT NULL,
  "LineTotal" numeric(18,2) NOT NULL, "CreatedAt" timestamptz NOT NULL,
  "UpdatedAt" timestamptz NOT NULL,
  CONSTRAINT "FK_InvoiceItems_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE CASCADE);
CREATE INDEX IF NOT EXISTS "IX_InvoiceItems_InvoiceId" ON "InvoiceItems" ("InvoiceId");
""");

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("InvoiceItems"); m.DropTable("Invoices"); m.DropTable("RefreshTokens");
        m.DropTable("Expenses"); m.DropTable("Products"); m.DropTable("Customers");
        m.DropTable("Users"); m.DropTable("Tenants");
    }
}
