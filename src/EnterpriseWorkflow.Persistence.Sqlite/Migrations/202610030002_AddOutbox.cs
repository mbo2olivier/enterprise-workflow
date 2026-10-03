using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;

[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610030002_AddOutbox")]
public sealed class AddOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        CREATE TABLE "EwOutbox" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_EwOutbox" PRIMARY KEY,
            "InstanceId" TEXT NOT NULL,
            "ActivationId" TEXT NOT NULL,
            "OperationId" TEXT COLLATE BINARY NOT NULL,
            "Destination" TEXT COLLATE BINARY NOT NULL,
            "ContentType" TEXT NOT NULL,
            "PayloadJson" TEXT NOT NULL,
            "IdempotencyKey" TEXT NOT NULL,
            "Status" INTEGER NOT NULL,
            "Attempt" INTEGER NOT NULL,
            "DueAtUnixMilliseconds" INTEGER NOT NULL,
            "OwnerId" TEXT COLLATE BINARY NULL,
            "Generation" INTEGER NOT NULL,
            "LeaseToken" TEXT NULL,
            "LeaseExpiresAtUnixMilliseconds" INTEGER NULL,
            "LastErrorCode" TEXT NULL,
            "CreatedAtUnixMilliseconds" INTEGER NOT NULL,
            "DeliveredAtUnixMilliseconds" INTEGER NULL,
            CONSTRAINT "FK_EwOutbox_EwInstances" FOREIGN KEY ("InstanceId")
                REFERENCES "EwInstances" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_EwOutbox_EwActivations" FOREIGN KEY ("ActivationId")
                REFERENCES "EwActivations" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX "UX_EwOutbox_Activation_Operation"
            ON "EwOutbox" ("ActivationId", "OperationId");
        CREATE INDEX "IX_EwOutbox_Status_Due"
            ON "EwOutbox" ("Status", "DueAtUnixMilliseconds");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE \"EwOutbox\";");
}
