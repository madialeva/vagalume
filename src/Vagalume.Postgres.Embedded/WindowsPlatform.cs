namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Windows x64 strategy: files inherit the ACL of the user profile, so no explicit restriction is applied.
/// </summary>
public sealed class WindowsPlatform : IPlatformStrategy
{
    public string Id => "windows-amd64";

    public string ExecutableName(string name) => name + ".exe";

    public void RestrictToOwner(string path)
    {
    }

    public void EnsureCanRunServer()
    {
    }
}
