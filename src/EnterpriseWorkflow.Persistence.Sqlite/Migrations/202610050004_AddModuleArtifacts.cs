using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;

[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610050004_AddModuleArtifacts")]
public sealed class AddModuleArtifacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        CREATE TABLE "EwModuleArtifacts" (
            "ModuleId" TEXT COLLATE BINARY NOT NULL,
            "Version" TEXT COLLATE BINARY NOT NULL,
            "Sha256" TEXT NOT NULL,
            "InstalledAtUnixMilliseconds" INTEGER NOT NULL,
            CONSTRAINT "PK_EwModuleArtifacts" PRIMARY KEY ("ModuleId", "Version")
        );
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("EwModuleArtifacts");
}
