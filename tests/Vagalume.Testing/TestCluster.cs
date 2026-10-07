using Vagalume.Postgres.Embedded;

namespace Vagalume.Testing;

/// <summary>
/// A host over a temporary data directory that is stopped and deleted when the test ends.
/// </summary>
public sealed class TestCluster : IAsyncDisposable
{
    private readonly string _root = TestEnvironment.NewTempDirectory();

    public TestCluster(string? name = null)
    {
        DataDirectory = Path.Combine(_root, name ?? "data");
        Host = NewHost(DataDirectory);
    }

    public string DataDirectory { get; }

    public EmbeddedPostgresHost Host { get; }

    public string NewSiblingDirectory(string name) => Path.Combine(_root, name);

    public static EmbeddedPostgresHost NewHost(string dataDirectory) =>
        EmbeddedPostgresHost.Create(new EmbeddedPostgresOptions(TestEnvironment.BinariesDirectory, dataDirectory));

    public async ValueTask DisposeAsync()
    {
        foreach (var directory in Directory.Exists(_root) ? Directory.GetDirectories(_root) : [])
        {
            if (File.Exists(Path.Combine(directory, "PG_VERSION")))
            {
                await NewHost(directory).StopAsync(CancellationToken.None);
            }
        }

        Npgsql.NpgsqlConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }
}
