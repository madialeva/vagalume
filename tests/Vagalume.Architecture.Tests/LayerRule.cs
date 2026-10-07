namespace Vagalume.Architecture.Tests;

/// <summary>
/// Dependencies one project may have: the projects it may reference and the packages it must never reference.
/// </summary>
public sealed record LayerRule(
    string Project,
    IReadOnlySet<string> AllowedProjects,
    IReadOnlyList<string> ForbiddenPackagePrefixes,
    bool AllowFrameworks = false)
{
    public IEnumerable<string> Check(ProjectDependencies dependencies)
    {
        foreach (var project in dependencies.Projects.Where(p => !AllowedProjects.Contains(p)))
        {
            yield return $"{Project} must not reference project {project}.";
        }

        foreach (var package in dependencies.Packages.Where(IsForbidden))
        {
            yield return $"{Project} must not reference package {package}.";
        }

        if (!AllowFrameworks)
        {
            foreach (var framework in dependencies.Frameworks)
            {
                yield return $"{Project} must not reference framework {framework}.";
            }
        }
    }

    private bool IsForbidden(string package) =>
        ForbiddenPackagePrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
