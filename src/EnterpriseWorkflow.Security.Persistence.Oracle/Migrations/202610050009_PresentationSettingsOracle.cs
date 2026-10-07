using EnterpriseWorkflow.Security.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Security.Persistence.Oracle.Migrations;

[DbContext(typeof(SecurityDbContext)), Migration("202610050009_PresentationSettingsOracle")]
public sealed class PresentationSettingsOracle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("EwPresentationSettings", columns => new
        {
            Id = columns.Column<string>(maxLength: 32),
            DisplayName = columns.Column<string>(maxLength: 160),
            LogoResourcePath = columns.Column<string>(maxLength: 512, nullable: true),
            AccentColor = columns.Column<string>(maxLength: 7),
            TimeZoneId = columns.Column<string>(maxLength: 128),
            Culture = columns.Column<string>(maxLength: 32),
            Revision = columns.Column<long>(),
        }, constraints: table => table.PrimaryKey("PK_EwPresentationSettings", row => row.Id));
        migrationBuilder.Sql("INSERT INTO \"EwPresentationSettings\" (\"Id\", \"DisplayName\", \"LogoResourcePath\", \"AccentColor\", \"TimeZoneId\", \"Culture\", \"Revision\") VALUES ('global', 'Enterprise Workflow', NULL, '#1768E5', 'UTC', 'fr', 0)");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("EwPresentationSettings");
}
