using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Persistence.Oracle.Migrations;

[DbContext(typeof(OracleWorkflowDbContext))]
[Migration("202610250006_AddTatProjections")]
public sealed class AddTatProjections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_INSTANCES" ("Id" VARCHAR2(32 CHAR) NOT NULL, "ProcessId" VARCHAR2(128 CHAR) NOT NULL, "ProcessVersion" NUMBER(10) NOT NULL, "InitiatorProviderId" VARCHAR2(128 CHAR) NOT NULL, "InitiatorSubjectId" VARCHAR2(512 CHAR) NOT NULL, "StartedAtMs" NUMBER(19) NOT NULL, "EndedAtMs" NUMBER(19), "Status" NUMBER(10) NOT NULL, "SourceRevision" NUMBER(19) NOT NULL, "Coverage" NUMBER(10) NOT NULL, PRIMARY KEY ("Id"))""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_STEPS" ("Id" VARCHAR2(32 CHAR) NOT NULL, "InstanceId" VARCHAR2(32 CHAR) NOT NULL, "NodeId" VARCHAR2(128 CHAR) NOT NULL, "StartedAtMs" NUMBER(19) NOT NULL, "EndedAtMs" NUMBER(19), "Status" NUMBER(10) NOT NULL, "Attempt" NUMBER(10) NOT NULL, "Coverage" NUMBER(10) NOT NULL, PRIMARY KEY ("Id"), FOREIGN KEY ("InstanceId") REFERENCES "EW_TAT_INSTANCES" ("Id"))""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_ASSIGNMENTS" ("Id" VARCHAR2(128 CHAR) NOT NULL, "InstanceId" VARCHAR2(32 CHAR) NOT NULL, "StepId" VARCHAR2(32 CHAR) NOT NULL, "TaskId" VARCHAR2(32 CHAR) NOT NULL, "ActorProviderId" VARCHAR2(128 CHAR), "ActorSubjectId" VARCHAR2(512 CHAR), "StartedAtMs" NUMBER(19) NOT NULL, "EndedAtMs" NUMBER(19), "EndReason" VARCHAR2(64 CHAR), "SourceRevision" NUMBER(19) NOT NULL, "Coverage" NUMBER(10) NOT NULL, PRIMARY KEY ("Id"), FOREIGN KEY ("InstanceId") REFERENCES "EW_TAT_INSTANCES" ("Id"))""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE TABLE "EW_TAT_EVENTS" ("Id" VARCHAR2(64 CHAR) NOT NULL, "InstanceId" VARCHAR2(32 CHAR) NOT NULL, "SourceId" VARCHAR2(32 CHAR) NOT NULL, "SourceKind" VARCHAR2(32 CHAR) NOT NULL, "EventType" VARCHAR2(64 CHAR) NOT NULL, "AtMs" NUMBER(19) NOT NULL, "SourceRevision" NUMBER(19) NOT NULL, "Status" NUMBER(10) NOT NULL, "ActorProviderId" VARCHAR2(128 CHAR), "ActorSubjectId" VARCHAR2(512 CHAR), PRIMARY KEY ("Id"), FOREIGN KEY ("InstanceId") REFERENCES "EW_TAT_INSTANCES" ("Id"))""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_0" ON "EW_TAT_INSTANCES" ("ProcessId", "ProcessVersion", "StartedAtMs")""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_1" ON "EW_TAT_STEPS" ("InstanceId", "NodeId")""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_2" ON "EW_TAT_ASSIGNMENTS" ("ActorProviderId", "ActorSubjectId", "StartedAtMs")""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_3" ON "EW_TAT_ASSIGNMENTS" ("TaskId", "EndedAtMs")""", suppressTransaction: true);
        migrationBuilder.Sql("""CREATE INDEX "IX_EW_TAT_4" ON "EW_TAT_EVENTS" ("InstanceId", "AtMs")""", suppressTransaction: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_EVENTS" """, suppressTransaction: true);
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_ASSIGNMENTS" """, suppressTransaction: true);
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_STEPS" """, suppressTransaction: true);
        migrationBuilder.Sql("""DROP TABLE "EW_TAT_INSTANCES" """, suppressTransaction: true);
    }
}
