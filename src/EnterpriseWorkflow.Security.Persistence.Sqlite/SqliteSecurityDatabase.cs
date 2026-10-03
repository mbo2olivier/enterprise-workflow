using Microsoft.EntityFrameworkCore;
using EnterpriseWorkflow.Security.Persistence;

namespace EnterpriseWorkflow.Security.Persistence.Sqlite;

public sealed class SqliteSecurityDatabase
{
    private readonly DbContextOptions<SecurityDbContext> _options;
    public SqliteSecurityDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseSqlite(connectionString, x => { x.MigrationsAssembly(typeof(SqliteSecurityDatabase).Assembly.FullName); x.MigrationsHistoryTable("__EwSecurityMigrationsHistory"); }).Options;
    }
    public SecurityDbContext CreateDbContext() => new(_options);
    public RelationalSecurityStore CreateStore(TimeProvider? timeProvider = null) => new(CreateDbContext, timeProvider ?? TimeProvider.System);
    public async Task MigrateAsync(CancellationToken cancellationToken = default) { await using var db = CreateDbContext(); await db.Database.MigrateAsync(cancellationToken); }
}
