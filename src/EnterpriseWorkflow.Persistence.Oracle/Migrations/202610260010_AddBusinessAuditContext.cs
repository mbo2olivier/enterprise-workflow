using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Oracle.Migrations;
[DbContext(typeof(OracleWorkflowDbContext))]
[Migration("202610260010_AddBusinessAuditContext")]
public sealed class AddBusinessAuditContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("NODE_ID", "EW_AUDITS", type: "VARCHAR2(128 CHAR)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("ACTION_ID", "EW_AUDITS", type: "VARCHAR2(128 CHAR)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("ACTOR_DISPLAY_NAME", "EW_AUDITS", type: "VARCHAR2(512 CHAR)", maxLength: 512, nullable: true);
        migrationBuilder.AddColumn<string>("OUTCOME", "EW_AUDITS", type: "VARCHAR2(32 CHAR)", maxLength: 32, nullable: false, defaultValue: "unknown");
        migrationBuilder.CreateIndex("IX_EW_AUDIT_SEARCH", "EW_AUDITS", ["OCCURRED_AT_MS", "SEQUENCE"]);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_EW_AUDIT_SEARCH", "EW_AUDITS");
        migrationBuilder.DropColumn("NODE_ID", "EW_AUDITS");
        migrationBuilder.DropColumn("ACTION_ID", "EW_AUDITS");
        migrationBuilder.DropColumn("ACTOR_DISPLAY_NAME", "EW_AUDITS");
        migrationBuilder.DropColumn("OUTCOME", "EW_AUDITS");
    }
}
