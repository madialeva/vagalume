using Microsoft.EntityFrameworkCore;

namespace Vagalume.Data;

/// <summary>
/// Creates one short-lived <see cref="VagalumeDbContext"/> per operation, which keeps Blazor circuits free of stale tracked state.
/// </summary>
public sealed class VagalumeDbContextFactory : IDbContextFactory<VagalumeDbContext>
{
    private readonly DbContextOptions<VagalumeDbContext> _options;

    public VagalumeDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<VagalumeDbContext>().UseNpgsql(connectionString).Options;
    }

    public VagalumeDbContext CreateDbContext() => new(_options);
}
