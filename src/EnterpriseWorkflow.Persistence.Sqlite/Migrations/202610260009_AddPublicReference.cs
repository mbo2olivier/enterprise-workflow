using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;
[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610260009_AddPublicReference")]
public sealed class AddPublicReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("PublicReference", "EwInstances", type: "TEXT", maxLength: 10, nullable: false, defaultValue: "0000000000");
        migrationBuilder.Sql("""UPDATE "EwInstances" SET "PublicReference" = (SELECT printf('%010d', n) FROM (SELECT "Id", ROW_NUMBER() OVER (ORDER BY "Id") n FROM "EwInstances") numbered WHERE numbered."Id" = "EwInstances"."Id")""");
        migrationBuilder.CreateIndex("UX_EW_PUBLIC_REFERENCE", "EwInstances", "PublicReference", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("UX_EW_PUBLIC_REFERENCE", "EwInstances");
        migrationBuilder.DropColumn("PublicReference", "EwInstances");
    }
}
