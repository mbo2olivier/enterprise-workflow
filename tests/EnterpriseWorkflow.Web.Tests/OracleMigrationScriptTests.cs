using EnterpriseWorkflow.Security.Persistence.Oracle;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class OracleMigrationScriptTests
{
    [Fact]
    public void L11SecurityMigrationsGenerateOracle19CompatibleSqlWithoutConnecting()
    {
        // Script generation is an offline check, not a claim of Oracle integration qualification.
        var database = new OracleSecurityDatabase("User Id=unused;Password=unused;Data Source=unused");
        using var context = database.CreateDbContext();
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("EwUserCultures", script, StringComparison.Ordinal);
        Assert.Contains("SecondFactorVerified", script, StringComparison.Ordinal);
        Assert.DoesNotContain(" BOOLEAN", script, StringComparison.OrdinalIgnoreCase);
    }
}
