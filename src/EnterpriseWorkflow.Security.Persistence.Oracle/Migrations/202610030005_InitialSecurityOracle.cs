using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EnterpriseWorkflow.Security.Persistence;

namespace EnterpriseWorkflow.Security.Persistence.Oracle.Migrations;

[DbContext(typeof(SecurityDbContext)), Migration("202610030005_InitialSecurityOracle")]
public sealed class InitialSecurityOracle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => SecurityMigration.BuildUp(migrationBuilder);
    protected override void Down(MigrationBuilder migrationBuilder) => SecurityMigration.BuildDown(migrationBuilder);
}
