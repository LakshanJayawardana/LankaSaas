using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608150022_PlatformSubscriptionPlans")]
public sealed class PlatformSubscriptionPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SubscriptionPlans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                MonthlyPriceLkr = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                UserLimit = table.Column<int>(type: "integer", nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SubscriptionPlans", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_SubscriptionPlans_Code", table: "SubscriptionPlans", column: "Code", unique: true);

        migrationBuilder.AddColumn<string>(name: "PlanName", table: "PaymentOrders", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<int>(name: "PlanUserLimit", table: "PaymentOrders", type: "integer", nullable: false, defaultValue: 1);

        migrationBuilder.Sql("""
            INSERT INTO "SubscriptionPlans" ("Id","Code","Name","MonthlyPriceLkr","UserLimit","Description","IsActive","CreatedAt","UpdatedAt") VALUES
            ('10000000-0000-0000-0000-000000000001','Starter','Starter',2500.00,5,'For small teams getting started.',TRUE,NOW(),NOW()),
            ('10000000-0000-0000-0000-000000000002','Growth','Growth',6500.00,20,'For growing businesses with more staff.',TRUE,NOW(),NOW()),
            ('10000000-0000-0000-0000-000000000003','Business','Business',15000.00,50,'For established businesses needing more access.',TRUE,NOW(),NOW());
            UPDATE "PaymentOrders" SET "PlanName"="Plan", "PlanUserLimit"=CASE "Plan" WHEN 'Starter' THEN 5 WHEN 'Growth' THEN 20 WHEN 'Business' THEN 50 ELSE 1 END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PlanName", table: "PaymentOrders");
        migrationBuilder.DropColumn(name: "PlanUserLimit", table: "PaymentOrders");
        migrationBuilder.DropTable(name: "SubscriptionPlans");
    }
}
