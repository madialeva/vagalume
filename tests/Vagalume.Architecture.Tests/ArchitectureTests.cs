using Vagalume.Testing;

namespace Vagalume.Architecture.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Solution_RespectsLayerRules()
    {
        var violations = SolutionRules.Check(Path.Combine(TestEnvironment.RepoRoot, "src"));

        Assert.Empty(violations);
    }

    [Fact]
    public void ForbiddenReference_IsDetectedAndNamed()
    {
        var root = TestEnvironment.NewTempDirectory();
        try
        {
            foreach (var rule in SolutionRules.All)
            {
                var directory = Directory.CreateDirectory(Path.Combine(root, rule.Project));
                var core = rule.Project == "Vagalume.Core"
                    ? "<ProjectReference Include=\"..\\Vagalume.Data\\Vagalume.Data.csproj\" /><PackageReference Include=\"Npgsql\" Version=\"1\" />"
                    : string.Empty;
                File.WriteAllText(
                    Path.Combine(directory.FullName, rule.Project + ".csproj"),
                    $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{core}</ItemGroup></Project>");
            }

            var violations = SolutionRules.Check(root);

            Assert.Contains("Vagalume.Core must not reference project Vagalume.Data.", violations);
            Assert.Contains("Vagalume.Core must not reference package Npgsql.", violations);
            Assert.Equal(2, violations.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
