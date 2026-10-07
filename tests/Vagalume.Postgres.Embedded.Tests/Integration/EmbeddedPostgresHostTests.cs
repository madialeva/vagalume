using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Npgsql;
using Vagalume.Postgres.Embedded;
using Xunit.Abstractions;

namespace Vagalume.Postgres.Embedded.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class EmbeddedPostgresHostTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly ITestOutputHelper _output;

    public EmbeddedPostgresHostTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task FullCycle_StartsWritesReadsAndStopsCleanly()
    {
        await using var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);

        await ExecuteAsync(cluster.Host, "CREATE TABLE invoice (id int PRIMARY KEY, total numeric(12,2))");
        await ExecuteAsync(cluster.Host, "INSERT INTO invoice VALUES (1, 1234.56)");
        Assert.Equal(1234.56m, await ScalarAsync<decimal>(cluster.Host, "SELECT total FROM invoice WHERE id = 1"));
        Assert.Equal("UTF8", await ScalarAsync<string>(cluster.Host, "SHOW server_encoding"));

        NpgsqlConnection.ClearAllPools();
        await cluster.Host.StopAsync(Ct);
        Assert.False(await cluster.Host.IsRunningAsync(Ct));
        Assert.False(File.Exists(Path.Combine(cluster.DataDirectory, "postmaster.pid")));
    }

    [Fact]
    public async Task RestartedHost_DoesNotReinitializeAndKeepsData()
    {
        await using var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        await ExecuteAsync(cluster.Host, "CREATE TABLE t (v int)");
        await ExecuteAsync(cluster.Host, "INSERT INTO t VALUES (7)");
        var secret = File.ReadAllText(Path.Combine(cluster.DataDirectory, "vagalume.secret"));
        NpgsqlConnection.ClearAllPools();
        await cluster.Host.StopAsync(Ct);

        var second = TestCluster.NewHost(cluster.DataDirectory);
        await second.StartAsync(Ct);

        Assert.Equal(7, await ScalarAsync<int>(second, "SELECT v FROM t"));
        Assert.Equal(secret, File.ReadAllText(Path.Combine(cluster.DataDirectory, "vagalume.secret")));
    }

    [Fact]
    public async Task Access_IsRestrictedToLoopbackWithPassword()
    {
        await using var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        var builder = new NpgsqlConnectionStringBuilder(cluster.Host.ConnectionString);

        var withoutPassword = new NpgsqlConnectionStringBuilder(cluster.Host.ConnectionString) { Password = null, Pooling = false };
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var connection = new NpgsqlConnection(withoutPassword.ConnectionString);
            await connection.OpenAsync(Ct);
        });

        var wrongPassword = new NpgsqlConnectionStringBuilder(cluster.Host.ConnectionString) { Password = "wrong", Pooling = false };
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = new NpgsqlConnection(wrongPassword.ConnectionString);
            await connection.OpenAsync(Ct);
        });

        var pid = File.ReadAllLines(Path.Combine(cluster.DataDirectory, "postmaster.pid"));
        Assert.Equal("127.0.0.1", pid[5]);
        Assert.Equal(string.Empty, pid[4]);

        var hba = File.ReadAllLines(Path.Combine(cluster.DataDirectory, "pg_hba.conf"))
            .Where(l => !l.TrimStart().StartsWith('#') && l.Trim().Length > 0);
        Assert.DoesNotContain(hba, l => l.Contains("trust", StringComparison.Ordinal));

        var externalAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(i => i.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        foreach (var address in externalAddresses)
        {
            using var client = new TcpClient();
            await Assert.ThrowsAnyAsync<SocketException>(() => client.ConnectAsync(address, builder.Port, Ct).AsTask());
        }

        if (OperatingSystem.IsLinux())
        {
            Assert.Empty(Directory.GetFiles("/tmp", $".s.PGSQL.{builder.Port}*"));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(cluster.DataDirectory, "vagalume.secret")));
        }
    }

    [Fact]
    public async Task ForcedKill_RecoversCommittedDataWithoutSecondServer()
    {
        await using var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        await ExecuteAsync(cluster.Host, "CREATE TABLE t (v int)");
        await ExecuteAsync(cluster.Host, "INSERT INTO t VALUES (42)");
        NpgsqlConnection.ClearAllPools();

        var pidFile = Path.Combine(cluster.DataDirectory, "postmaster.pid");
        var oldPid = int.Parse(File.ReadAllLines(pidFile)[0]);
        using (var postmaster = Process.GetProcessById(oldPid))
        {
            postmaster.Kill(entireProcessTree: true);
            postmaster.WaitForExit();
        }

        Assert.True(File.Exists(pidFile), "a stale postmaster.pid should remain after a forced kill");
        Assert.False(await cluster.Host.IsRunningAsync(Ct));

        var restarted = TestCluster.NewHost(cluster.DataDirectory);
        await restarted.StartAsync(Ct);

        Assert.Equal(42, await ScalarAsync<int>(restarted, "SELECT v FROM t"));
        var newPid = int.Parse(File.ReadAllLines(pidFile)[0]);
        Assert.NotEqual(oldPid, newPid);
        if (OperatingSystem.IsLinux())
        {
            Assert.Equal(1, CountPostmasters(cluster.DataDirectory));
        }
    }

    [Fact]
    public async Task SecondHost_ReusesRunningServer()
    {
        await using var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        var pidFile = Path.Combine(cluster.DataDirectory, "postmaster.pid");
        var pid = File.ReadAllLines(pidFile)[0];

        var second = TestCluster.NewHost(cluster.DataDirectory);
        await second.StartAsync(Ct);

        Assert.Equal(cluster.Host.ConnectionString, second.ConnectionString);
        Assert.Equal(pid, File.ReadAllLines(pidFile)[0]);
        if (OperatingSystem.IsLinux())
        {
            Assert.Equal(1, CountPostmasters(cluster.DataDirectory));
        }
    }

    [Fact]
    public async Task BackupAndRestore_ReproducesDataInNewDirectory()
    {
        await using var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        await ExecuteAsync(cluster.Host, "CREATE TABLE t (id int, name text)");
        await ExecuteAsync(cluster.Host, "INSERT INTO t VALUES (1, 'uno'), (2, 'dos')");
        NpgsqlConnection.ClearAllPools();

        var backup = cluster.NewSiblingDirectory("backup");
        await cluster.Host.BackupAsync(backup, Ct);
        Assert.True(await cluster.Host.IsRunningAsync(Ct));

        var restoredData = cluster.NewSiblingDirectory("restored");
        var restored = TestCluster.NewHost(restoredData);
        restored.Restore(backup);
        await restored.StartAsync(Ct);

        Assert.NotEqual(cluster.Host.ConnectionString, restored.ConnectionString);
        Assert.Equal(2, await ScalarAsync<long>(restored, "SELECT count(*) FROM t"));
        Assert.Equal("dos", await ScalarAsync<string>(restored, "SELECT name FROM t WHERE id = 2"));
    }

    [Fact]
    public async Task Measurements_AreReportedForTheFindings()
    {
        _output.WriteLine($"platform: {TestEnvironment.Platform.Id}");
        _output.WriteLine($"distribution size: {DirectorySize(TestEnvironment.BinariesDirectory) / 1_048_576.0:F1} MiB");

        await using var cluster = new TestCluster();
        var stopwatch = Stopwatch.StartNew();
        await cluster.Host.StartAsync(Ct);
        _output.WriteLine($"first start (initdb + start + create database): {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"data directory size after init: {DirectorySize(cluster.DataDirectory) / 1_048_576.0:F1} MiB");
        NpgsqlConnection.ClearAllPools();
        await cluster.Host.StopAsync(Ct);

        stopwatch.Restart();
        await TestCluster.NewHost(cluster.DataDirectory).StartAsync(Ct);
        _output.WriteLine($"restart on existing cluster: {stopwatch.ElapsedMilliseconds} ms");
    }

    private static long DirectorySize(string path) =>
        Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);

    private static int CountPostmasters(string dataDirectory) =>
        Directory.GetDirectories("/proc")
            .Where(d => int.TryParse(Path.GetFileName(d), out _))
            .Select(d =>
            {
                try
                {
                    return File.ReadAllText(Path.Combine(d, "cmdline")).Replace('\0', ' ');
                }
                catch (IOException)
                {
                    return string.Empty;
                }
            })
            .Count(c => c.Contains("postgres", StringComparison.Ordinal) && c.Contains("-D " + dataDirectory, StringComparison.Ordinal));

    private static async Task ExecuteAsync(EmbeddedPostgresHost host, string sql)
    {
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<T> ScalarAsync<T>(EmbeddedPostgresHost host, string sql)
    {
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(Ct))!;
    }
}
