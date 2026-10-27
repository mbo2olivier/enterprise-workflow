using EnterpriseWorkflow.Security.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EnterpriseWorkflow.Security.Persistence.Oracle.Migrations;
[DbContext(typeof(SecurityDbContext)), Migration("202610250010_AuditSearchIndex")]
public sealed class AuditSearchIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex("IX_EW_SEC_AUDIT_CURSOR", "EwSecurityAudit", ["OccurredAtUnixMilliseconds", "Id"]);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropIndex("IX_EW_SEC_AUDIT_CURSOR", "EwSecurityAudit");
}
