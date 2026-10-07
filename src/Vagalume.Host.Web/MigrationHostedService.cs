using Npgsql;
using Vagalume.Data;

namespace Vagalume.Host.Web;

/// <summary>
/// Applies the database migrations while the host starts and turns connection failures into a readable <see cref="StartupException"/>.
/// </summary>
public sealed class MigrationHostedService : IHostedService
{
    private readonly DatabaseMigrator _migrator;

    public MigrationHostedService(DatabaseMigrator migrator)
    {
        _migrator = migrator;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _migrator.MigrateAsync(cancellationToken);
        }
        catch (Exception error) when (error is NpgsqlException or System.Net.Sockets.SocketException)
        {
            throw new StartupException(
                $"Could not connect to the database configured in '{VagalumeWebHost.ConnectionStringKey}': {error.Message}",
                error);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
