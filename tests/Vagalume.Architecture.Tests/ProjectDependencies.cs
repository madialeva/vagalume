using System.Xml.Linq;

namespace Vagalume.Architecture.Tests;

/// <summary>
/// What a project file declares: the projects, packages and framework it references.
/// </summary>
public sealed record ProjectDependencies(
    string Name,
    IReadOnlySet<string> Projects,
    IReadOnlySet<string> Packages,
    IReadOnlySet<string> Frameworks)
{
    public static ProjectDependencies Read(string projectFile)
    {
        var document = XDocument.Load(projectFile);
        return new ProjectDependencies(
            Path.GetFileNameWithoutExtension(projectFile),
            Collect(document, "ProjectReference", e => Path.GetFileNameWithoutExtension(Normalize(e.Attribute("Include")?.Value))),
            Collect(document, "PackageReference", e => e.Attribute("Include")?.Value ?? string.Empty),
            Collect(document, "FrameworkReference", e => e.Attribute("Include")?.Value ?? string.Empty));
    }

    private static HashSet<string> Collect(XDocument document, string element, Func<XElement, string> select) =>
        document.Descendants(element).Select(select).ToHashSet(StringComparer.Ordinal);

    private static string Normalize(string? path) => (path ?? string.Empty).Replace('\\', '/');
}
