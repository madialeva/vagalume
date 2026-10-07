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
            ["Vagalume.Core", "Vagalume.Data", "Vagalume.UI", "Vagalume.Postgres.Embedded", "Vagalume.Desktop.Hosting"],
            [],
            allowFrameworks: true),
        Rule(
            "Vagalume.Host.Wasm.Desktop",
            ["Vagalume.Core", "Vagalume.Data", "Vagalume.Api", "Vagalume.Wasm.UI", "Vagalume.Postgres.Embedded", "Vagalume.Desktop.Hosting"],
            [],
            allowFrameworks: true),
        Rule(
            "Vagalume.Desktop.Hosting",
            ["Vagalume.Data", "Vagalume.Postgres.Embedded"],
            [.. Container, .. Electron],
            allowFrameworks: true),
        Rule("Vagalume.Api.Contracts", [], [.. Container, .. Electron, .. Persistence]),
        Rule("Vagalume.Api", ["Vagalume.Core", "Vagalume.Api.Contracts"], [.. Container, .. Electron, .. Persistence], allowFrameworks: true),
        Rule("Vagalume.Api.Client", ["Vagalume.Api.Contracts"], [.. Container, .. Electron, .. Persistence]),
        Rule(
            "Vagalume.Wasm.UI",
            ["Vagalume.Api.Contracts", "Vagalume.Api.Client"],
            [.. Container, .. Electron, .. Persistence]),
    ];

    /// <summary>Projects that must be written in C# only, without Razor syntax.</summary>
    public static IReadOnlyList<string> RazorFreeProjects { get; } = ["Vagalume.Wasm.UI"];

    public static IReadOnlyList<string> CheckNoRazorFiles(string sourceDirectory, IEnumerable<string> projects)
    {
        var violations = new List<string>();
        foreach (var project in projects)
        {
            var directory = Path.Combine(sourceDirectory, project);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            violations.AddRange(
                Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                    .Where(f => !IsBuildOutput(directory, f))
                    .Select(f => $"{project} must not contain Razor file {Path.GetRelativePath(sourceDirectory, f)}."));
        }

        return violations;
    }

    private static bool IsBuildOutput(string projectDirectory, string file)
    {
        var first = Path.GetRelativePath(projectDirectory, file).Split(Path.DirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }

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
