using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;
[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610260010_AddBusinessAuditContext")]
public sealed class AddBusinessAuditContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("NodeId", "EwAudits", type: "TEXT", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("ActionId", "EwAudits", type: "TEXT", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("ActorDisplayName", "EwAudits", type: "TEXT", maxLength: 512, nullable: true);
        migrationBuilder.AddColumn<string>("Outcome", "EwAudits", type: "TEXT", maxLength: 32, nullable: false, defaultValue: "unknown");
        migrationBuilder.CreateIndex("IX_EW_AUDIT_SEARCH", "EwAudits", ["OccurredAtUnixMilliseconds", "Sequence"]);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_EW_AUDIT_SEARCH", "EwAudits");
        migrationBuilder.DropColumn("NodeId", "EwAudits");
        migrationBuilder.DropColumn("ActionId", "EwAudits");
        migrationBuilder.DropColumn("ActorDisplayName", "EwAudits");
        migrationBuilder.DropColumn("Outcome", "EwAudits");
    }
}
