namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Paths of the executables of an extracted PostgreSQL distribution.
/// </summary>
public sealed record PostgresBinaries(string InitDb, string PgCtl)
{
    public static PostgresBinaries Locate(string binariesDirectory, IPlatformStrategy platform)
    {
        var bin = Path.Combine(binariesDirectory, "bin");
        var binaries = new PostgresBinaries(
            Path.Combine(bin, platform.ExecutableName("initdb")),
            Path.Combine(bin, platform.ExecutableName("pg_ctl")));
        foreach (var path in new[] { binaries.InitDb, binaries.PgCtl })
        {
            if (!File.Exists(path))
            {
                throw new EmbeddedPostgresException(
                    $"PostgreSQL binary not found: {path}. Build the solution to fetch the binaries.");
            }
        }

        return binaries;
    }
}
