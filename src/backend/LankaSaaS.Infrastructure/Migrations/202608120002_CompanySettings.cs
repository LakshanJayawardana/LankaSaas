using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608120002_CompanySettings")]
public sealed class CompanySettings : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "TaxRegistrationNumber" text NULL;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "InvoicePrefix" text NOT NULL DEFAULT 'INV';
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "NextInvoiceNumber" integer NOT NULL DEFAULT 1;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "DefaultPaymentTermsDays" integer NOT NULL DEFAULT 14;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "DefaultTaxRate" numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "InvoiceFooter" text NULL;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "PaymentInstructions" text NULL;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "LogoUrl" text NULL;
UPDATE "Tenants" t SET "NextInvoiceNumber"=COALESCE((SELECT COUNT(*)+1 FROM "Invoices" i WHERE i."TenantId"=t."Id"),1)
WHERE "NextInvoiceNumber"=1;
""");
    protected override void Down(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "LogoUrl";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "PaymentInstructions";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "InvoiceFooter";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "DefaultTaxRate";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "DefaultPaymentTermsDays";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "NextInvoiceNumber";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "InvoicePrefix";
ALTER TABLE "Tenants" DROP COLUMN IF EXISTS "TaxRegistrationNumber";
""");
}
