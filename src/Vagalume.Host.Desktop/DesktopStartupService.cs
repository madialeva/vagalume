using Vagalume.Data;
using Vagalume.Postgres.Embedded;

namespace Vagalume.Host.Desktop;

/// <summary>
/// Starts the embedded PostgreSQL and migrates the schema before the web server accepts requests,
/// and stops the PostgreSQL after the web server has stopped.
/// </summary>
public sealed class DesktopStartupService : IHostedService
{
    private readonly EmbeddedPostgresHost _postgres;
    private readonly EmbeddedDatabase _database;
    private readonly IServiceProvider _services;

    public DesktopStartupService(EmbeddedPostgresHost postgres, EmbeddedDatabase database, IServiceProvider services)
    {
        _postgres = postgres;
        _database = database;
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _postgres.StartAsync(cancellationToken);
        _database.ConnectionString = _postgres.ConnectionString;
        await _services.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => _postgres.StopAsync(cancellationToken);
}
