using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;
[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610250008_AddInstanceSearchIndexes")]
public sealed class AddInstanceSearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex("IX_EW_SEARCH_CREATED", "EwInstances", ["CreatedAtUnixMilliseconds", "Id"]);
        migrationBuilder.CreateIndex("IX_EW_SEARCH_INITIATOR", "EwInstances", ["InitiatorProviderId", "InitiatorSubjectId", "CreatedAtUnixMilliseconds"]);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_EW_SEARCH_CREATED", "EwInstances");
        migrationBuilder.DropIndex("IX_EW_SEARCH_INITIATOR", "EwInstances");
    }
}
