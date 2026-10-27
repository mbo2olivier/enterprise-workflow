using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Oracle.Migrations;
[DbContext(typeof(OracleWorkflowDbContext))]
[Migration("202610260009_AddPublicReference")]
public sealed class AddPublicReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("PUBLIC_REFERENCE", "EW_INSTANCES", type: "VARCHAR2(10 CHAR)", maxLength: 10, nullable: false, defaultValue: "0000000000");
        migrationBuilder.Sql("""MERGE INTO "EW_INSTANCES" target USING (SELECT "ID", LPAD(TO_CHAR(ROW_NUMBER() OVER (ORDER BY "ID")), 10, '0') PUBLIC_REF FROM "EW_INSTANCES") source ON (target."ID" = source."ID") WHEN MATCHED THEN UPDATE SET target."PUBLIC_REFERENCE" = source.PUBLIC_REF""");
        migrationBuilder.CreateIndex("UX_EW_PUBLIC_REFERENCE", "EW_INSTANCES", "PUBLIC_REFERENCE", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("UX_EW_PUBLIC_REFERENCE", "EW_INSTANCES");
        migrationBuilder.DropColumn("PUBLIC_REFERENCE", "EW_INSTANCES");
    }
}
