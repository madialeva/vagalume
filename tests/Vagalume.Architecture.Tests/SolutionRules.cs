namespace Vagalume.Architecture.Tests;

/// <summary>
/// The layering rules of the solution: Core knows nothing, Data and UI know only Core,
/// and only the hosts compose everything else.
/// </summary>
public static class SolutionRules
{
    private static readonly string[] Container = ["Microsoft.Extensions.DependencyInjection"];
    private static readonly string[] Electron = ["ElectronNET"];
    private static readonly string[] Persistence = ["Microsoft.EntityFrameworkCore", "Npgsql"];

    public static IReadOnlyList<LayerRule> All { get; } =
    [
        Rule("Vagalume.Core", [], [.. Container, .. Electron, .. Persistence]),
        Rule("Vagalume.Data", ["Vagalume.Core"], [.. Container, .. Electron]),
        Rule("Vagalume.UI", ["Vagalume.Core"], [.. Container, .. Electron, .. Persistence], allowFrameworks: true),
        Rule("Vagalume.Postgres.Embedded", [], [.. Electron]),
        Rule("Vagalume.Host.Web", ["Vagalume.Core", "Vagalume.Data", "Vagalume.UI"], Electron, allowFrameworks: true),
        Rule(
            "Vagalume.Host.Desktop",
            ["Vagalume.Core", "Vagalume.Data", "Vagalume.UI", "Vagalume.Postgres.Embedded"],
            [],
            allowFrameworks: true),
    ];

    public static IReadOnlyList<string> Check(string sourceDirectory)
    {
        var violations = new List<string>();
        foreach (var rule in All)
        {
            var file = Path.Combine(sourceDirectory, rule.Project, rule.Project + ".csproj");
            if (!File.Exists(file))
            {
                violations.Add($"Project file not found: {file}");
                continue;
            }

            violations.AddRange(rule.Check(ProjectDependencies.Read(file)));
        }

        return violations;
    }

    private static LayerRule Rule(string project, string[] allowedProjects, string[] forbiddenPackages, bool allowFrameworks = false) =>
        new(project, allowedProjects.ToHashSet(StringComparer.Ordinal), forbiddenPackages, allowFrameworks);
}
