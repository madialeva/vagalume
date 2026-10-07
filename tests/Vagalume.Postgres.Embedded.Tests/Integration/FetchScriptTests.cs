using Vagalume.Postgres.Embedded;

namespace Vagalume.Postgres.Embedded.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class FetchScriptTests
{
    private static readonly string Script = Path.Combine(TestEnvironment.RepoRoot, "eng", "fetch-postgres-binaries.cs");
    private static readonly string Lock = Path.Combine(TestEnvironment.RepoRoot, "eng", "postgres-binaries.lock");

    [Fact]
    public async Task TamperedHash_FailsAndExtractsNothing()
    {
        var work = TestEnvironment.NewTempDirectory();
        try
        {
            var tampered = Path.Combine(work, "tampered.lock");
            File.WriteAllLines(tampered, File.ReadAllLines(Lock).Select(l => TamperHash(l, TestEnvironment.Platform.Id)));
            var output = Path.Combine(work, "out");

            var result = await Fetch(tampered, output);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("mismatch", result.Error, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(output, TestEnvironment.Platform.Id)));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public async Task BinariesAlreadyPresent_NeedNoNetwork()
    {
        var work = TestEnvironment.NewTempDirectory();
        try
        {
            var unreachable = Path.Combine(work, "unreachable.lock");
            File.WriteAllLines(unreachable, File.ReadAllLines(Lock).Select(MakeUnreachable));

            var result = await Fetch(unreachable, Path.Combine(TestEnvironment.RepoRoot, "artifacts", "postgres"));

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("up to date", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void Distribution_ContainsOnlyItsOwnPlatform()
    {
        var bin = Path.Combine(TestEnvironment.BinariesDirectory, "bin");
        var otherSuffix = OperatingSystem.IsWindows() ? string.Empty : ".exe";

        Assert.True(File.Exists(Path.Combine(bin, TestEnvironment.Platform.ExecutableName("postgres"))));
        Assert.False(File.Exists(Path.Combine(bin, "postgres" + otherSuffix)));
    }

    private static string TamperHash(string line, string platform)
    {
        if (!line.StartsWith(platform + '\t', StringComparison.Ordinal))
        {
            return line;
        }

        var parts = line.Split('\t');
        parts[2] = new string('0', 64);
        return string.Join('\t', parts);
    }

    private static string MakeUnreachable(string line)
    {
        if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var parts = line.Split('\t');
        parts[3] = "https://127.0.0.1:1/unreachable.jar";
        return string.Join('\t', parts);
    }

    private static Task<ProcessResult> Fetch(string lockFile, string output) =>
        new ProcessRunner().RunAsync(
            "dotnet", [Script, lockFile, output, TestEnvironment.Platform.Id], CancellationToken.None);
}
