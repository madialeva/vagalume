using Vagalume.Postgres.Embedded;

namespace Vagalume.Postgres.Embedded.Tests.Unit;

public sealed class PlatformAndHelpersTests
{
    [Fact]
    public void LinuxPlatform_AsRoot_RejectsWithClearError()
    {
        var platform = new LinuxPlatform(() => true);

        var error = Assert.Throws<EmbeddedPostgresException>(platform.EnsureCanRunServer);

        Assert.Contains("root", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LinuxPlatform_AsRegularUser_IsAccepted()
    {
        new LinuxPlatform(() => false).EnsureCanRunServer();
    }

    [Fact]
    public void WindowsPlatform_AddsExeSuffix()
    {
        Assert.Equal("pg_ctl.exe", new WindowsPlatform().ExecutableName("pg_ctl"));
        Assert.Equal("pg_ctl", new LinuxPlatform(() => false).ExecutableName("pg_ctl"));
    }

    [Fact]
    public void RandomSecretGenerator_ReturnsDistinctHexSecrets()
    {
        var generator = new RandomSecretGenerator();

        var first = generator.Generate();

        Assert.Equal(64, first.Length);
        Assert.All(first, c => Assert.True(Uri.IsHexDigit(c)));
        Assert.NotEqual(first, generator.Generate());
    }

    [Fact]
    public void LoopbackPortFinder_ReturnsUsablePort()
    {
        Assert.InRange(new LoopbackPortFinder().FindFreePort(), 1, 65535);
    }

    [Fact]
    public void DirectoryCopier_CopiesNestedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "vagalume-copy-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var source = Path.Combine(root, "src");
            Directory.CreateDirectory(Path.Combine(source, "a", "b"));
            File.WriteAllText(Path.Combine(source, "a", "b", "f.txt"), "hello");

            DirectoryCopier.Copy(source, Path.Combine(root, "dst"));

            Assert.Equal("hello", File.ReadAllText(Path.Combine(root, "dst", "a", "b", "f.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
