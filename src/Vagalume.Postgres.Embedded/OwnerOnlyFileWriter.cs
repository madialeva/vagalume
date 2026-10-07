namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Writes files that must never be readable by other users, restricting them before any content is written.
/// </summary>
public sealed class OwnerOnlyFileWriter
{
    private readonly IPlatformStrategy _platform;

    public OwnerOnlyFileWriter(IPlatformStrategy platform)
    {
        _platform = platform;
    }

    public void Write(string path, string content)
    {
        File.WriteAllBytes(path, []);
        _platform.RestrictToOwner(path);
        File.WriteAllText(path, content);
    }
}
