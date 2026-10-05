using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Oracle.Migrations;

[DbContext(typeof(OracleWorkflowDbContext))]
[Migration("202610050005_AddModuleArtifacts")]
public sealed class AddModuleArtifacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "EW_MODULE_ARTIFACTS" (
                "MODULE_ID" VARCHAR2(128 CHAR) NOT NULL,
                "MODULE_VERSION" VARCHAR2(128 CHAR) NOT NULL,
                "SHA256" CHAR(64 CHAR) NOT NULL,
                "INSTALLED_AT_MS" NUMBER(19) NOT NULL,
                CONSTRAINT "PK_EW_MODULE_ARTIFACTS" PRIMARY KEY ("MODULE_ID", "MODULE_VERSION")
            )
            """, suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE \"EW_MODULE_ARTIFACTS\" CASCADE CONSTRAINTS PURGE", suppressTransaction: true);
}
