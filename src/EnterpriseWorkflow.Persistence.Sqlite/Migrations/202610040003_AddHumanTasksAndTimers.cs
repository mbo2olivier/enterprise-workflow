using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;

[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610040003_AddHumanTasksAndTimers")]
public sealed class AddHumanTasksAndTimers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        ALTER TABLE "EwInstances" ADD COLUMN "InitiatorProviderId" TEXT COLLATE BINARY NOT NULL DEFAULT 'system';
        ALTER TABLE "EwInstances" ADD COLUMN "InitiatorSubjectId" TEXT COLLATE BINARY NOT NULL DEFAULT 'system';

        CREATE TABLE "EwHumanTasks" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_EwHumanTasks" PRIMARY KEY,
            "ActivationId" TEXT NOT NULL,
            "InstanceId" TEXT NOT NULL,
            "NodeId" TEXT COLLATE BINARY NOT NULL,
            "Status" INTEGER NOT NULL,
            "AssignmentMode" INTEGER NOT NULL,
            "AssigneeProviderId" TEXT COLLATE BINARY NULL,
            "AssigneeSubjectId" TEXT COLLATE BINARY NULL,
            "Revision" INTEGER NOT NULL,
            "CreatedAtUnixMilliseconds" INTEGER NOT NULL,
            "CompletedAtUnixMilliseconds" INTEGER NULL,
            "CompletedActionId" TEXT COLLATE BINARY NULL,
            "CompletedByProviderId" TEXT COLLATE BINARY NULL,
            "CompletedBySubjectId" TEXT COLLATE BINARY NULL,
            CONSTRAINT "FK_EwHumanTasks_Activation" FOREIGN KEY ("ActivationId") REFERENCES "EwActivations" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_EwHumanTasks_Instance" FOREIGN KEY ("InstanceId") REFERENCES "EwInstances" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX "UX_EwHumanTasks_Activation" ON "EwHumanTasks" ("ActivationId");
        CREATE INDEX "IX_EwHumanTasks_Status_Created" ON "EwHumanTasks" ("Status", "CreatedAtUnixMilliseconds", "Id");
        CREATE INDEX "IX_EwHumanTasks_Instance" ON "EwHumanTasks" ("InstanceId");

        CREATE TABLE "EwHumanTaskReceipts" (
            "ReceiptKey" TEXT NOT NULL CONSTRAINT "PK_EwHumanTaskReceipts" PRIMARY KEY,
            "TaskId" TEXT NOT NULL,
            "IdempotencyKey" TEXT COLLATE BINARY NOT NULL,
            "ActorProviderId" TEXT COLLATE BINARY NOT NULL,
            "ActorSubjectId" TEXT COLLATE BINARY NOT NULL,
            "ActionId" TEXT COLLATE BINARY NOT NULL,
            "RequestSha256" TEXT NOT NULL,
            "TaskRevision" INTEGER NOT NULL,
            "InstanceRevision" INTEGER NOT NULL,
            "CommittedAtUnixMilliseconds" INTEGER NOT NULL,
            CONSTRAINT "FK_EwHumanTaskReceipts_Task" FOREIGN KEY ("TaskId") REFERENCES "EwHumanTasks" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX "UX_EwHumanTaskReceipts_Task_Key" ON "EwHumanTaskReceipts" ("TaskId", "IdempotencyKey");

        CREATE TABLE "EwTimers" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_EwTimers" PRIMARY KEY,
            "ActivationId" TEXT NOT NULL,
            "InstanceId" TEXT NOT NULL,
            "NodeId" TEXT COLLATE BINARY NOT NULL,
            "NextNodeId" TEXT COLLATE BINARY NOT NULL,
            "Status" INTEGER NOT NULL,
            "DueAtUnixMilliseconds" INTEGER NOT NULL,
            "Revision" INTEGER NOT NULL,
            "CreatedAtUnixMilliseconds" INTEGER NOT NULL,
            "FiredAtUnixMilliseconds" INTEGER NULL,
            CONSTRAINT "FK_EwTimers_Activation" FOREIGN KEY ("ActivationId") REFERENCES "EwActivations" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_EwTimers_Instance" FOREIGN KEY ("InstanceId") REFERENCES "EwInstances" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX "UX_EwTimers_Activation" ON "EwTimers" ("ActivationId");
        CREATE INDEX "IX_EwTimers_Status_Due" ON "EwTimers" ("Status", "DueAtUnixMilliseconds");

        CREATE TABLE "EwDesignatedAssignments" (
            "InstanceId" TEXT NOT NULL,
            "NodeId" TEXT COLLATE BINARY NOT NULL,
            "AssigneeProviderId" TEXT COLLATE BINARY NOT NULL,
            "AssigneeSubjectId" TEXT COLLATE BINARY NOT NULL,
            CONSTRAINT "PK_EwDesignatedAssignments" PRIMARY KEY ("InstanceId", "NodeId"),
            CONSTRAINT "FK_EwDesignatedAssignments_Instance" FOREIGN KEY ("InstanceId") REFERENCES "EwInstances" ("Id") ON DELETE CASCADE
        );
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        DROP TABLE "EwHumanTaskReceipts";
        DROP TABLE "EwDesignatedAssignments";
        DROP TABLE "EwTimers";
        DROP TABLE "EwHumanTasks";
        ALTER TABLE "EwInstances" DROP COLUMN "InitiatorSubjectId";
        ALTER TABLE "EwInstances" DROP COLUMN "InitiatorProviderId";
        """);
}
