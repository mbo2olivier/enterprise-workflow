using EnterpriseWorkflow.Security.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Security.Persistence.Oracle.Migrations;

[DbContext(typeof(SecurityDbContext)), Migration("202610040007_WorkflowAccessOracle")]
public sealed class WorkflowAccessOracle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var m = migrationBuilder;
        m.CreateTable("EwWorkflowPolicyState", c => new { Id = c.Column<string>(maxLength: 32), Revision = c.Column<long>() }, constraints: t => t.PrimaryKey("PK_EwWfPolicyState", x => x.Id));
        m.Sql("INSERT INTO \"EwWorkflowPolicyState\" (\"Id\", \"Revision\") VALUES ('global', 0)");
        m.CreateTable("EwWorkflowAccessGrants", c => new
        {
            GrantId = c.Column<string>(maxLength: 64), WorkflowId = c.Column<string>(maxLength: 128), DefinitionVersion = c.Column<int>(),
            NodeId = c.Column<string>(maxLength: 128), ActionId = c.Column<string>(maxLength: 160), RecipientKind = c.Column<int>(),
            RecipientProviderId = c.Column<string>(maxLength: 256, nullable: true), RecipientSubjectId = c.Column<string>(maxLength: 256, nullable: true),
            RecipientProfileId = c.Column<string>(maxLength: 160, nullable: true), PolicyRevision = c.Column<long>(),
        }, constraints: t => t.PrimaryKey("PK_EwWfAccessGrants", x => x.GrantId));
        m.CreateIndex("IX_EwWfGrantScope", "EwWorkflowAccessGrants", ["WorkflowId", "DefinitionVersion", "NodeId", "ActionId"]);
        m.CreateIndex("IX_EwWfGrantIdentity", "EwWorkflowAccessGrants", ["RecipientProviderId", "RecipientSubjectId"]);
        m.CreateIndex("IX_EwWfGrantProfile", "EwWorkflowAccessGrants", "RecipientProfileId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("EwWorkflowAccessGrants"); migrationBuilder.DropTable("EwWorkflowPolicyState"); }
}
