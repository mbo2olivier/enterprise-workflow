using EnterpriseWorkflow.Security.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Security.Persistence.Sqlite.Migrations;

[DbContext(typeof(SecurityDbContext)), Migration("202611180012_SecondFactorSession")]
public sealed class SecondFactorSession : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>(
        "SecondFactorVerified", "EwSecuritySessions", nullable: false, defaultValue: false);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("SecondFactorVerified", "EwSecuritySessions");
}
