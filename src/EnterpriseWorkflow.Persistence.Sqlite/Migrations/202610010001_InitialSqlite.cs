using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;

[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610010001_InitialSqlite")]
public sealed class InitialSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "EwDefinitions" (
                "DefinitionId" TEXT COLLATE BINARY NOT NULL,
                "Version" INTEGER NOT NULL,
                "Sha256" TEXT NOT NULL,
                "SchemaVersion" INTEGER NOT NULL,
                "CanonicalJson" TEXT NOT NULL,
                "PublishedAtUnixMilliseconds" INTEGER NOT NULL,
                CONSTRAINT "PK_EwDefinitions" PRIMARY KEY ("DefinitionId", "Version")
            );

            CREATE TABLE "EwInstances" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_EwInstances" PRIMARY KEY,
                "DefinitionId" TEXT COLLATE BINARY NOT NULL,
                "DefinitionVersion" INTEGER NOT NULL,
                "DefinitionSha256" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "Revision" INTEGER NOT NULL,
                "StateSchemaVersion" INTEGER NOT NULL,
                "StateJson" TEXT NOT NULL,
                "BusinessKey" TEXT NULL,
                "CorrelationId" TEXT NULL,
                "CreatedAtUnixMilliseconds" INTEGER NOT NULL,
                "UpdatedAtUnixMilliseconds" INTEGER NOT NULL,
                CONSTRAINT "FK_EwInstances_EwDefinitions" FOREIGN KEY ("DefinitionId", "DefinitionVersion")
                    REFERENCES "EwDefinitions" ("DefinitionId", "Version") ON DELETE RESTRICT
            );

            CREATE TABLE "EwActivations" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_EwActivations" PRIMARY KEY,
                "InstanceId" TEXT NOT NULL,
                "NodeId" TEXT COLLATE BINARY NOT NULL,
                "Status" INTEGER NOT NULL,
                "Attempt" INTEGER NOT NULL,
                "ErrorCode" TEXT NULL,
                CONSTRAINT "FK_EwActivations_EwInstances" FOREIGN KEY ("InstanceId")
                    REFERENCES "EwInstances" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "EwWorkItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_EwWorkItems" PRIMARY KEY,
                "ActivationId" TEXT NOT NULL,
                "InstanceId" TEXT NOT NULL,
                "NodeId" TEXT COLLATE BINARY NOT NULL,
                "Status" INTEGER NOT NULL,
                "DueAtUnixMilliseconds" INTEGER NOT NULL,
                "OwnerId" TEXT COLLATE BINARY NULL,
                "Generation" INTEGER NOT NULL,
                "LeaseToken" TEXT NULL,
                "LeaseExpiresAtUnixMilliseconds" INTEGER NULL,
                "CreatedAtUnixMilliseconds" INTEGER NOT NULL,
                CONSTRAINT "FK_EwWorkItems_EwActivations" FOREIGN KEY ("ActivationId")
                    REFERENCES "EwActivations" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_EwWorkItems_EwInstances" FOREIGN KEY ("InstanceId")
                    REFERENCES "EwInstances" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "EwStartReceipts" (
                "InstallationId" TEXT COLLATE BINARY NOT NULL,
                "CommandTypeId" TEXT COLLATE BINARY NOT NULL,
                "ActorProviderId" TEXT COLLATE BINARY NOT NULL,
                "ActorSubjectId" TEXT COLLATE BINARY NOT NULL,
                "IdempotencyKey" TEXT COLLATE BINARY NOT NULL,
                "RequestSha256" TEXT NOT NULL,
                "InstanceId" TEXT NOT NULL,
                "InstanceRevision" INTEGER NOT NULL,
                "CommittedAtUnixMilliseconds" INTEGER NOT NULL,
                CONSTRAINT "PK_EwStartReceipts" PRIMARY KEY
                    ("InstallationId", "CommandTypeId", "ActorProviderId", "ActorSubjectId", "IdempotencyKey"),
                CONSTRAINT "FK_EwStartReceipts_EwInstances" FOREIGN KEY ("InstanceId")
                    REFERENCES "EwInstances" ("Id") ON DELETE RESTRICT
            );

            CREATE TABLE "EwAudits" (
                "Sequence" INTEGER NOT NULL CONSTRAINT "PK_EwAudits" PRIMARY KEY AUTOINCREMENT,
                "InstanceId" TEXT NOT NULL,
                "EventType" TEXT NOT NULL,
                "Revision" INTEGER NOT NULL,
                "OccurredAtUnixMilliseconds" INTEGER NOT NULL,
                "ActorProviderId" TEXT COLLATE BINARY NULL,
                "ActorSubjectId" TEXT COLLATE BINARY NULL,
                CONSTRAINT "FK_EwAudits_EwInstances" FOREIGN KEY ("InstanceId")
                    REFERENCES "EwInstances" ("Id") ON DELETE CASCADE
            );

            CREATE INDEX "IX_EwInstances_Definition_Status"
                ON "EwInstances" ("DefinitionId", "DefinitionVersion", "Status");
            CREATE INDEX "IX_EwActivations_InstanceId" ON "EwActivations" ("InstanceId");
            CREATE UNIQUE INDEX "UX_EwWorkItems_ActivationId" ON "EwWorkItems" ("ActivationId");
            CREATE INDEX "IX_EwWorkItems_Status_Due" ON "EwWorkItems" ("Status", "DueAtUnixMilliseconds");
            CREATE INDEX "IX_EwWorkItems_Status_LeaseExpiry"
                ON "EwWorkItems" ("Status", "LeaseExpiresAtUnixMilliseconds");
            CREATE INDEX "IX_EwAudits_Instance_Sequence" ON "EwAudits" ("InstanceId", "Sequence");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE "EwAudits";
            DROP TABLE "EwStartReceipts";
            DROP TABLE "EwWorkItems";
            DROP TABLE "EwActivations";
            DROP TABLE "EwInstances";
            DROP TABLE "EwDefinitions";
            """);
    }
}
