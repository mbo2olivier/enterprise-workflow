using EnterpriseWorkflow.Security.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Security.Persistence.Sqlite.Migrations;

[DbContext(typeof(SecurityDbContext)), Migration("202611180011_UserCulturePreference")]
public sealed class UserCulturePreference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        "EwUserCultures",
        columns => new
        {
            ProviderId = columns.Column<string>(maxLength: 256, nullable: false),
            SubjectId = columns.Column<string>(maxLength: 256, nullable: false),
            Culture = columns.Column<string>(maxLength: 32, nullable: false),
        }, constraints: table => table.PrimaryKey("PK_EwUserCultures", row => new { row.ProviderId, row.SubjectId }));
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("EwUserCultures");
}
