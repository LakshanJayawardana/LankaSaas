using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608140019_UserAccessVersion")]
public sealed class UserAccessVersion:Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.AddColumn<int>(name:"AccessVersion",table:"Users",type:"integer",nullable:false,defaultValue:0);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.DropColumn(name:"AccessVersion",table:"Users");
}
