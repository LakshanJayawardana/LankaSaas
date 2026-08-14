using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608140020_PlatformAdministration")]
public sealed class PlatformAdministration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name:"PlatformUsers",columns:table=>new
        {
            Id=table.Column<Guid>(type:"uuid",nullable:false),
            Email=table.Column<string>(type:"text",nullable:false),
            PasswordHash=table.Column<string>(type:"text",nullable:false),
            Role=table.Column<string>(type:"text",nullable:false),
            IsActive=table.Column<bool>(type:"boolean",nullable:false),
            AccessVersion=table.Column<int>(type:"integer",nullable:false),
            LastLoginAt=table.Column<DateTimeOffset>(type:"timestamp with time zone",nullable:true),
            CreatedAt=table.Column<DateTimeOffset>(type:"timestamp with time zone",nullable:false),
            UpdatedAt=table.Column<DateTimeOffset>(type:"timestamp with time zone",nullable:false)
        },constraints:table=>table.PrimaryKey("PK_PlatformUsers",x=>x.Id));
        migrationBuilder.CreateTable(name:"PlatformAuditEvents",columns:table=>new
        {
            Id=table.Column<Guid>(type:"uuid",nullable:false),
            PlatformUserId=table.Column<Guid>(type:"uuid",nullable:false),
            TargetTenantId=table.Column<Guid>(type:"uuid",nullable:true),
            Action=table.Column<string>(type:"text",nullable:false),
            Description=table.Column<string>(type:"text",nullable:false),
            CorrelationId=table.Column<string>(type:"text",nullable:false),
            IpAddress=table.Column<string>(type:"text",nullable:true),
            CreatedAt=table.Column<DateTimeOffset>(type:"timestamp with time zone",nullable:false),
            UpdatedAt=table.Column<DateTimeOffset>(type:"timestamp with time zone",nullable:false)
        },constraints:table=>table.PrimaryKey("PK_PlatformAuditEvents",x=>x.Id));
        migrationBuilder.CreateIndex(name:"IX_PlatformUsers_Email",table:"PlatformUsers",column:"Email",unique:true);
        migrationBuilder.CreateIndex(name:"IX_PlatformAuditEvents_TargetTenantId_CreatedAt",table:"PlatformAuditEvents",columns:new[]{"TargetTenantId","CreatedAt"});
        migrationBuilder.CreateIndex(name:"IX_PlatformAuditEvents_PlatformUserId_CreatedAt",table:"PlatformAuditEvents",columns:new[]{"PlatformUserId","CreatedAt"});
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name:"PlatformAuditEvents");
        migrationBuilder.DropTable(name:"PlatformUsers");
    }
}
