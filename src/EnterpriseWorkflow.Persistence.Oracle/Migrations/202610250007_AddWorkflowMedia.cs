using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Oracle.Migrations;
[DbContext(typeof(OracleWorkflowDbContext))]
[Migration("202610250007_AddWorkflowMedia")]
public sealed class AddWorkflowMedia : Migration {
protected override void Up(MigrationBuilder migrationBuilder) {
migrationBuilder.Sql("""CREATE TABLE "EW_MEDIA" ("Key" VARCHAR2(64 CHAR) NOT NULL, "MediaId" VARCHAR2(32 CHAR) NOT NULL, "Revision" NUMBER(10) NOT NULL, "Name" VARCHAR2(256 CHAR) NOT NULL, "ContentType" VARCHAR2(128 CHAR) NOT NULL, "Length" NUMBER(19) NOT NULL, "Sha256" VARCHAR2(64 CHAR) NOT NULL, "ObjectKey" VARCHAR2(128 CHAR) NOT NULL, "OwnerProviderId" VARCHAR2(128 CHAR) NOT NULL, "OwnerSubjectId" VARCHAR2(512 CHAR) NOT NULL, "InstanceId" VARCHAR2(32 CHAR), "ActivationId" VARCHAR2(32 CHAR), "SubmissionKey" VARCHAR2(128 CHAR), "SourceField" VARCHAR2(128 CHAR), "Hidden" NUMBER(10) NOT NULL, "PermissionGroupsJson" VARCHAR2(4000 CHAR) NOT NULL, "Status" NUMBER(10) NOT NULL, "CreatedAtMs" NUMBER(19) NOT NULL, PRIMARY KEY ("Key"), UNIQUE ("MediaId", "Revision"))""", suppressTransaction: true);
migrationBuilder.Sql("""CREATE INDEX "IX_EW_MEDIA_INSTANCE" ON "EW_MEDIA" ("InstanceId", "Status")""", suppressTransaction: true);
}
protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""DROP TABLE "EW_MEDIA" """, suppressTransaction: true);
}
