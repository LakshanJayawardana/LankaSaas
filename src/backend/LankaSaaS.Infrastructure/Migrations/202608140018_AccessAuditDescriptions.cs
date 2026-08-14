using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608140018_AccessAuditDescriptions")]
public sealed class AccessAuditDescriptions:Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.AddColumn<string>(name:"Description",table:"AuditEvents",type:"text",nullable:true);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.DropColumn(name:"Description",table:"AuditEvents");
}
