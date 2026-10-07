namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Runs <c>initdb</c> once for a new cluster and stores its generated secret.
/// </summary>
public sealed class ClusterInitializer
{
    private readonly string _initDb;
    private readonly EmbeddedPostgresOptions _options;
    private readonly IProcessRunner _runner;
    private readonly ISecretGenerator _secrets;
    private readonly IPlatformStrategy _platform;

    public ClusterInitializer(
        string initDb,
        EmbeddedPostgresOptions options,
        IProcessRunner runner,
        ISecretGenerator secrets,
        IPlatformStrategy platform)
    {
        _initDb = initDb;
        _options = options;
        _runner = runner;
        _secrets = secrets;
        _platform = platform;
    }

    public async Task InitializeAsync(DataDirectoryLayout layout, CancellationToken cancellationToken)
    {
        if (!layout.IsEmpty)
        {
            throw new EmbeddedPostgresException(
                $"Data directory '{layout.Root}' is not empty and does not contain a cluster.");
        }

        var writer = new OwnerOnlyFileWriter(_platform);
        var secret = _secrets.Generate();
        var passwordFile = Path.Combine(Path.GetTempPath(), $"vagalume-{Guid.NewGuid():N}.pwd");
        writer.Write(passwordFile, secret);
        try
        {
            var result = await _runner.RunAsync(
                _initDb,
                [
                    "-D", layout.Root,
                    "-U", _options.UserName,
                    "-E", "UTF8",
                    "--locale=C",
                    "--locale-provider=icu",
                    $"--icu-locale={_options.IcuLocale}",
                    "--auth-local=scram-sha-256",
                    "--auth-host=scram-sha-256",
                    $"--pwfile={passwordFile}",
                ],
                cancellationToken);
            result.EnsureSuccess("initdb");
        }
        finally
        {
            File.Delete(passwordFile);
        }

        _platform.RestrictToOwner(layout.Root);
        File.AppendAllText(
            Path.Combine(layout.Root, "postgresql.conf"),
            $"{Environment.NewLine}include 'vagalume.conf'{Environment.NewLine}");
        writer.Write(layout.SecretFile, secret);
    }
}
