using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Sqlite;

/// <summary>Creates contexts and explicitly applies the SQLite workflow migrations.</summary>
public sealed class SqliteWorkflowDatabase
{
    private readonly DbContextOptions<SqliteWorkflowDbContext> _options;

    /// <summary>Creates a database handle for a SQLite connection string.</summary>
    public SqliteWorkflowDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            ForeignKeys = true,
            DefaultTimeout = 30,
        };
        ConnectionString = builder.ToString();
        _options = new DbContextOptionsBuilder<SqliteWorkflowDbContext>()
            .UseSqlite(ConnectionString, sqlite => sqlite.MigrationsAssembly(typeof(SqliteWorkflowDatabase).Assembly.FullName))
            .Options;
    }

    /// <summary>Gets the normalized provider connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Creates a short-lived context. The caller owns it.</summary>
    public SqliteWorkflowDbContext CreateDbContext() => new(_options);

    /// <summary>Applies pending, non-destructive workflow schema migrations.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
