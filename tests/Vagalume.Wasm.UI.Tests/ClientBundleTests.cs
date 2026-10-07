namespace Vagalume.Wasm.UI.Tests;

public sealed class ClientBundleTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Vagalume.Core",
        "Vagalume.Data",
        "Vagalume.Postgres",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
    ];

    [Fact]
    public void ClientBundle_NeverContainsTheServerOrDatabaseAssemblies()
    {
        var framework = FindFrameworkDirectory();

        var files = Directory.EnumerateFiles(framework).Select(Path.GetFileName).OfType<string>().ToList();

        Assert.Contains(files, f => f.StartsWith("Vagalume.Wasm.UI", StringComparison.Ordinal));
        Assert.Contains(files, f => f.StartsWith("Vagalume.Api.Client", StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => ForbiddenPrefixes.Any(prefix => f.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static string FindFrameworkDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vagalume.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        var output = Path.Combine(root, "src", "Vagalume.Wasm.UI", "bin");
        return Directory.EnumerateDirectories(output, "_framework", SearchOption.AllDirectories).First();
    }
}
