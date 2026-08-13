using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608130016_UserProfilePhotos")]
public sealed class UserProfilePhotos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "ProfilePhotoUrl" character varying(500) NULL;
""");

    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
ALTER TABLE "Users" DROP COLUMN IF EXISTS "ProfilePhotoUrl";
""");
}
