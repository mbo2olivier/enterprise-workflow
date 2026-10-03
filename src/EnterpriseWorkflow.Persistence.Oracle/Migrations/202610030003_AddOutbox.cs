using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Oracle.Migrations;

[DbContext(typeof(OracleWorkflowDbContext))]
[Migration("202610030003_AddOutbox")]
public sealed class AddOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Sql(migrationBuilder, """
            CREATE TABLE "EW_OUTBOX" (
                "ID" CHAR(32 CHAR) NOT NULL,
                "INSTANCE_ID" CHAR(32 CHAR) NOT NULL,
                "ACTIVATION_ID" CHAR(32 CHAR) NOT NULL,
                "OPERATION_ID" VARCHAR2(128 CHAR) NOT NULL,
                "DESTINATION" VARCHAR2(128 CHAR) NOT NULL,
                "CONTENT_TYPE" VARCHAR2(128 CHAR) NOT NULL,
                "PAYLOAD_JSON" CLOB NOT NULL,
                "IDEMPOTENCY_KEY" CHAR(64 CHAR) NOT NULL,
                "STATUS" NUMBER(10) NOT NULL,
                "ATTEMPT" NUMBER(10) NOT NULL,
                "DUE_AT_MS" NUMBER(19) NOT NULL,
                "OWNER_ID" VARCHAR2(128 CHAR) NULL,
                "GENERATION" NUMBER(19) NOT NULL,
                "LEASE_TOKEN" CHAR(32 CHAR) NULL,
                "LEASE_EXPIRES_AT_MS" NUMBER(19) NULL,
                "LAST_ERROR_CODE" VARCHAR2(128 CHAR) NULL,
                "CREATED_AT_MS" NUMBER(19) NOT NULL,
                "DELIVERED_AT_MS" NUMBER(19) NULL,
                CONSTRAINT "PK_EW_OUTBOX" PRIMARY KEY ("ID"),
                CONSTRAINT "FK_EW_OUTBOX_INSTANCE" FOREIGN KEY ("INSTANCE_ID")
                    REFERENCES "EW_INSTANCES" ("ID") ON DELETE CASCADE,
                CONSTRAINT "FK_EW_OUTBOX_ACTIVATION" FOREIGN KEY ("ACTIVATION_ID")
                    REFERENCES "EW_ACTIVATIONS" ("ID") ON DELETE CASCADE
            )
            """);
        Sql(migrationBuilder, "CREATE UNIQUE INDEX \"UX_EW_OUTBOX_ACT_OP\" ON \"EW_OUTBOX\" (\"ACTIVATION_ID\", \"OPERATION_ID\")");
        Sql(migrationBuilder, "CREATE INDEX \"IX_EW_OUTBOX_STATUS_DUE\" ON \"EW_OUTBOX\" (\"STATUS\", \"DUE_AT_MS\")");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        Sql(migrationBuilder, "DROP TABLE \"EW_OUTBOX\" CASCADE CONSTRAINTS PURGE");

    private static void Sql(MigrationBuilder migrationBuilder, string sql) =>
        migrationBuilder.Sql(sql, suppressTransaction: true);
}
