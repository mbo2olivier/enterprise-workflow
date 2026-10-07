using Microsoft.EntityFrameworkCore;
using EnterpriseWorkflow.Security.Persistence;

namespace EnterpriseWorkflow.Security.Persistence.Oracle;

public sealed class OracleSecurityDatabase
{
    private readonly DbContextOptions<SecurityDbContext> _options;
    public OracleSecurityDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseOracle(connectionString, x => { x.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19); x.MigrationsAssembly(typeof(OracleSecurityDatabase).Assembly.FullName); x.MigrationsHistoryTable("EW_SECURITY_MIGRATIONS"); }).Options;
    }
    public SecurityDbContext CreateDbContext() => new(_options);
    public RelationalSecurityStore CreateStore(TimeProvider? timeProvider = null) => new(CreateDbContext, timeProvider ?? TimeProvider.System);
    public async Task MigrateAsync(CancellationToken cancellationToken = default) { await using var db = CreateDbContext(); await db.Database.MigrateAsync(cancellationToken); }
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default) { await using var db = CreateDbContext(); return (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray(); }
}
