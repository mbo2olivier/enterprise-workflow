using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Oracle.Migrations;
[DbContext(typeof(OracleWorkflowDbContext))]
[Migration("202610250008_AddInstanceSearchIndexes")]
public sealed class AddInstanceSearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex("IX_EW_SEARCH_CREATED", "EW_INSTANCES", ["CREATED_AT_MS", "ID"]);
        migrationBuilder.CreateIndex("IX_EW_SEARCH_INITIATOR", "EW_INSTANCES", ["INITIATOR_PROVIDER_ID", "INITIATOR_SUBJECT_ID", "CREATED_AT_MS"]);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_EW_SEARCH_CREATED", "EW_INSTANCES");
        migrationBuilder.DropIndex("IX_EW_SEARCH_INITIATOR", "EW_INSTANCES");
    }
}
