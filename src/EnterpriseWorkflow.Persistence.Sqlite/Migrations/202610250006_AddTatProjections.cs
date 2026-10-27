using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;

[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610250006_AddTatProjections")]
public sealed class AddTatProjections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_INSTANCES" ("Id" TEXT NOT NULL, "ProcessId" TEXT NOT NULL, "ProcessVersion" INTEGER NOT NULL, "InitiatorProviderId" TEXT NOT NULL, "InitiatorSubjectId" TEXT NOT NULL, "StartedAtMs" INTEGER NOT NULL, "EndedAtMs" INTEGER, "Status" INTEGER NOT NULL, "SourceRevision" INTEGER NOT NULL, "Coverage" INTEGER NOT NULL, PRIMARY KEY ("Id"))""");
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_STEPS" ("Id" TEXT NOT NULL, "InstanceId" TEXT NOT NULL, "NodeId" TEXT NOT NULL, "StartedAtMs" INTEGER NOT NULL, "EndedAtMs" INTEGER, "Status" INTEGER NOT NULL, "Attempt" INTEGER NOT NULL, "Coverage" INTEGER NOT NULL, PRIMARY KEY ("Id"), FOREIGN KEY ("InstanceId") REFERENCES "EW_TAT_INSTANCES" ("Id"))""");
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_ASSIGNMENTS" ("Id" TEXT NOT NULL, "InstanceId" TEXT NOT NULL, "StepId" TEXT NOT NULL, "TaskId" TEXT NOT NULL, "ActorProviderId" TEXT, "ActorSubjectId" TEXT, "StartedAtMs" INTEGER NOT NULL, "EndedAtMs" INTEGER, "EndReason" TEXT, "SourceRevision" INTEGER NOT NULL, "Coverage" INTEGER NOT NULL, PRIMARY KEY ("Id"), FOREIGN KEY ("InstanceId") REFERENCES "EW_TAT_INSTANCES" ("Id"))""");
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_EVENTS" ("Id" TEXT NOT NULL, "InstanceId" TEXT NOT NULL, "SourceId" TEXT NOT NULL, "SourceKind" TEXT NOT NULL, "EventType" TEXT NOT NULL, "AtMs" INTEGER NOT NULL, "SourceRevision" INTEGER NOT NULL, "Status" INTEGER NOT NULL, "ActorProviderId" TEXT, "ActorSubjectId" TEXT, PRIMARY KEY ("Id"), FOREIGN KEY ("InstanceId") REFERENCES "EW_TAT_INSTANCES" ("Id"))""");
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_0" ON "EW_TAT_INSTANCES" ("ProcessId", "ProcessVersion", "StartedAtMs")""");
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_1" ON "EW_TAT_STEPS" ("InstanceId", "NodeId")""");
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_2" ON "EW_TAT_ASSIGNMENTS" ("ActorProviderId", "ActorSubjectId", "StartedAtMs")""");
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_3" ON "EW_TAT_ASSIGNMENTS" ("TaskId", "EndedAtMs")""");
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_4" ON "EW_TAT_EVENTS" ("InstanceId", "AtMs")""");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_EVENTS" """);
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_ASSIGNMENTS" """);
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_STEPS" """);
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_INSTANCES" """);
    }
}
