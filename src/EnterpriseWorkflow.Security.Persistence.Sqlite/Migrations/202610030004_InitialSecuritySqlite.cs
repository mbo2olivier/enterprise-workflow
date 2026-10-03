using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EnterpriseWorkflow.Security.Persistence;

namespace EnterpriseWorkflow.Security.Persistence.Sqlite.Migrations;

[DbContext(typeof(SecurityDbContext)), Migration("202610030004_InitialSecuritySqlite")]
public sealed class InitialSecuritySqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => SecurityMigration.BuildUp(migrationBuilder);
    protected override void Down(MigrationBuilder migrationBuilder) => SecurityMigration.BuildDown(migrationBuilder);
}
