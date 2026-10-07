using Vagalume.Postgres.Embedded;

namespace Vagalume.Postgres.Embedded.Tests.Integration;

/// <summary>
/// Locates the repository, the fetched binaries and creates throwaway directories for integration tests.
/// </summary>
internal static class TestEnvironment
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static IPlatformStrategy Platform { get; } = PlatformStrategies.Detect();

    public static string BinariesDirectory => Path.Combine(RepoRoot, "artifacts", "postgres", Platform.Id);

    public static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "vagalume-test-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vagalume.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
