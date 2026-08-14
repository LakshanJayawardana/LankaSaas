using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608140021_TenantArchival")]
public sealed class TenantArchival:Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name:"IsTestTenant",table:"Tenants",type:"boolean",nullable:false,defaultValue:false);
        migrationBuilder.AddColumn<bool>(name:"IsArchived",table:"Tenants",type:"boolean",nullable:false,defaultValue:false);
        migrationBuilder.AddColumn<DateTimeOffset>(name:"ArchivedAt",table:"Tenants",type:"timestamp with time zone",nullable:true);
        migrationBuilder.AddColumn<string>(name:"ArchivedReason",table:"Tenants",type:"text",nullable:true);
        migrationBuilder.CreateIndex(name:"IX_Tenants_IsTestTenant_IsArchived",table:"Tenants",columns:new[]{"IsTestTenant","IsArchived"});
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name:"IX_Tenants_IsTestTenant_IsArchived",table:"Tenants");
        migrationBuilder.DropColumn(name:"IsTestTenant",table:"Tenants");migrationBuilder.DropColumn(name:"IsArchived",table:"Tenants");migrationBuilder.DropColumn(name:"ArchivedAt",table:"Tenants");migrationBuilder.DropColumn(name:"ArchivedReason",table:"Tenants");
    }
}
