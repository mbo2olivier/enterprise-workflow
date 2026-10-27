using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Persistence.Sqlite.Migrations;
[DbContext(typeof(SqliteWorkflowDbContext))]
[Migration("202610250007_AddWorkflowMedia")]
public sealed class AddWorkflowMedia : Migration {
protected override void Up(MigrationBuilder migrationBuilder) {
migrationBuilder.Sql("""CREATE TABLE "EW_MEDIA" ("Key" TEXT NOT NULL, "MediaId" TEXT NOT NULL, "Revision" INTEGER NOT NULL, "Name" TEXT NOT NULL, "ContentType" TEXT NOT NULL, "Length" INTEGER NOT NULL, "Sha256" TEXT NOT NULL, "ObjectKey" TEXT NOT NULL, "OwnerProviderId" TEXT NOT NULL, "OwnerSubjectId" TEXT NOT NULL, "InstanceId" TEXT, "ActivationId" TEXT, "SubmissionKey" TEXT, "SourceField" TEXT, "Hidden" INTEGER NOT NULL, "PermissionGroupsJson" TEXT NOT NULL, "Status" INTEGER NOT NULL, "CreatedAtMs" INTEGER NOT NULL, PRIMARY KEY ("Key"), UNIQUE ("MediaId", "Revision"))""");
migrationBuilder.Sql("""CREATE INDEX "IX_EW_MEDIA_INSTANCE" ON "EW_MEDIA" ("InstanceId", "Status")""");
}
protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""DROP TABLE "EW_MEDIA" """);
}
