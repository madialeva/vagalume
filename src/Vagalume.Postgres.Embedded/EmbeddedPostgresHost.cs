using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;

namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Facade that owns the lifecycle of one embedded PostgreSQL cluster: first-time initialization,
/// start (or reuse of a running server), clean stop and file-level backup and restore.
/// </summary>
public sealed partial class EmbeddedPostgresHost
{
    private readonly EmbeddedPostgresOptions _options;
    private readonly IPlatformStrategy _platform;
    private readonly IFreePortFinder _ports;
    private readonly DataDirectoryLayout _layout;
    private readonly PgCtl _pgCtl;
    private readonly ClusterInitializer _initializer;
    private string? _connectionString;

    public EmbeddedPostgresHost(
        EmbeddedPostgresOptions options,
        IPlatformStrategy platform,
        IProcessRunner runner,
        IFreePortFinder ports,
        ISecretGenerator secrets)
    {
        if (!IdentifierPattern().IsMatch(options.DatabaseName) || !IdentifierPattern().IsMatch(options.UserName))
        {
            throw new ArgumentException("User and database names must be simple identifiers.", nameof(options));
        }

        _options = options;
        _platform = platform;
        _ports = ports;
        _layout = new DataDirectoryLayout(options.DataDirectory);
        var binaries = PostgresBinaries.Locate(options.BinariesDirectory, platform);
        _pgCtl = new PgCtl(binaries.PgCtl, runner);
        _initializer = new ClusterInitializer(binaries.InitDb, options, runner, secrets, platform);
    }

    /// <summary>Creates a host wired with the real collaborators of the running platform.</summary>
    public static EmbeddedPostgresHost Create(EmbeddedPostgresOptions options) =>
        new(options, PlatformStrategies.Detect(), new ProcessRunner(), new LoopbackPortFinder(), new RandomSecretGenerator());

    /// <summary>Connection string of the managed database; available once the host has started.</summary>
    public string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("The host has not been started.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _platform.EnsureCanRunServer();
        if (!_layout.IsInitialized)
        {
            await _initializer.InitializeAsync(_layout, cancellationToken);
        }

        if (!await _pgCtl.IsRunningAsync(_layout, cancellationToken))
        {
            var port = _ports.FindFreePort();
            File.WriteAllText(_layout.PortFile, port.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(
                _layout.HostConfigFile,
                $"listen_addresses = '127.0.0.1'{Environment.NewLine}" +
                $"unix_socket_directories = ''{Environment.NewLine}" +
                $"port = {port}{Environment.NewLine}");
            await _pgCtl.StartAsync(_layout, cancellationToken);
        }

        var activePort = _layout.ReadPort();
        var secret = _layout.ReadSecret();
        await EnsureDatabaseAsync(BuildConnectionString("postgres", activePort, secret), cancellationToken);
        _connectionString = BuildConnectionString(_options.DatabaseName, activePort, secret);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (await _pgCtl.IsRunningAsync(_layout, cancellationToken))
        {
            await _pgCtl.StopAsync(_layout, cancellationToken);
        }

        _connectionString = null;
    }

    public Task<bool> IsRunningAsync(CancellationToken cancellationToken) =>
        _pgCtl.IsRunningAsync(_layout, cancellationToken);

    /// <summary>Copies the whole cluster to <paramref name="backupDirectory"/> with the server stopped, then restarts it if it was running.</summary>
    public async Task BackupAsync(string backupDirectory, CancellationToken cancellationToken)
    {
        if (!new DataDirectoryLayout(backupDirectory).IsEmpty)
        {
            throw new EmbeddedPostgresException($"Backup directory '{backupDirectory}' is not empty.");
        }

        var wasRunning = await _pgCtl.IsRunningAsync(_layout, cancellationToken);
        if (wasRunning)
        {
            await StopAsync(cancellationToken);
        }

        DirectoryCopier.Copy(_layout.Root, backupDirectory);
        _platform.RestrictToOwner(backupDirectory);
        if (wasRunning)
        {
            await StartAsync(cancellationToken);
        }
    }

    /// <summary>Copies a backup into this host's empty data directory; call <see cref="StartAsync"/> afterwards.</summary>
    public void Restore(string backupDirectory)
    {
        if (!new DataDirectoryLayout(backupDirectory).IsInitialized)
        {
            throw new EmbeddedPostgresException($"'{backupDirectory}' does not contain a cluster backup.");
        }

        if (!_layout.IsEmpty)
        {
            throw new EmbeddedPostgresException($"Data directory '{_layout.Root}' is not empty.");
        }

        DirectoryCopier.Copy(backupDirectory, _layout.Root);
        _platform.RestrictToOwner(_layout.Root);
    }

    private string BuildConnectionString(string database, int port, string secret) =>
        $"Host=127.0.0.1;Port={port};Username={_options.UserName};Password={secret};Database={database}";

    private async Task EnsureDatabaseAsync(string maintenanceConnectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(maintenanceConnectionString + ";Pooling=false");
        await connection.OpenAsync(cancellationToken);
        await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        exists.Parameters.AddWithValue("name", _options.DatabaseName);
        if (await exists.ExecuteScalarAsync(cancellationToken) is null)
        {
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_options.DatabaseName}\"", connection);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex IdentifierPattern();
}
