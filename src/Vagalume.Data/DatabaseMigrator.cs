using Microsoft.EntityFrameworkCore;

namespace Vagalume.Data;

/// <summary>
/// Applies the single chain of migrations; a no-op when the schema is already up to date.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly IDbContextFactory<VagalumeDbContext> _contexts;

    public DatabaseMigrator(IDbContextFactory<VagalumeDbContext> contexts)
    {
        _contexts = contexts;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }
}
