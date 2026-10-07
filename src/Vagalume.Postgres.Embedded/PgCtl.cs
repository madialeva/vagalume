namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Facade over the <c>pg_ctl</c> executable: start, stop and status of a cluster.
/// </summary>
public sealed class PgCtl
{
    private const int NotRunningExitCode = 3;

    private readonly string _executable;
    private readonly IProcessRunner _runner;

    public PgCtl(string executable, IProcessRunner runner)
    {
        _executable = executable;
        _runner = runner;
    }

    public async Task<bool> IsRunningAsync(DataDirectoryLayout layout, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(_executable, ["status", "-D", layout.Root], cancellationToken);
        return result.ExitCode switch
        {
            0 => true,
            NotRunningExitCode or 4 => false,
            _ => throw new EmbeddedPostgresException($"pg_ctl status failed ({result.ExitCode}): {result.Output}{result.Error}"),
        };
    }

    public async Task StartAsync(DataDirectoryLayout layout, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            _executable, ["start", "-D", layout.Root, "-w", "-l", layout.LogFile], cancellationToken);
        if (result.ExitCode != 0)
        {
            var log = File.Exists(layout.LogFile) ? File.ReadAllText(layout.LogFile) : string.Empty;
            throw new EmbeddedPostgresException(
                $"pg_ctl start failed ({result.ExitCode}): {result.Output}{result.Error}{Environment.NewLine}{log}".TrimEnd());
        }
    }

    public async Task StopAsync(DataDirectoryLayout layout, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            _executable, ["stop", "-D", layout.Root, "-m", "fast", "-w"], cancellationToken);
        result.EnsureSuccess("pg_ctl stop");
    }
}
