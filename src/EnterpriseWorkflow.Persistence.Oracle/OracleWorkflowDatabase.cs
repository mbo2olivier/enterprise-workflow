using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace EnterpriseWorkflow.Persistence.Oracle;

/// <summary>Creates Oracle contexts and explicitly applies the provider-owned migrations.</summary>
public sealed class OracleWorkflowDatabase
{
    private readonly DbContextOptions<OracleWorkflowDbContext> _options;

    /// <summary>Creates a database handle for an Oracle connection string.</summary>
    public OracleWorkflowDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new OracleConnectionStringBuilder(connectionString);
        ConnectionString = builder.ConnectionString;
        _options = new DbContextOptionsBuilder<OracleWorkflowDbContext>()
            .UseOracle(ConnectionString, oracle =>
            {
                oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
                oracle.MigrationsAssembly(typeof(OracleWorkflowDatabase).Assembly.FullName);
            })
            .Options;
    }

    /// <summary>Gets the normalized connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Creates a short-lived context. The caller owns it.</summary>
    public OracleWorkflowDbContext CreateDbContext() => new(_options);

    /// <summary>Applies pending Oracle workflow migrations explicitly.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists migrations that would be applied without changing the database.</summary>
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext();
        return (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
    }
}
