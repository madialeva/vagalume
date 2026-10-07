namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Linux x64 strategy: owner-only Unix modes and a refusal to run the server as <c>root</c>.
/// </summary>
public sealed class LinuxPlatform : IPlatformStrategy
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = OwnerFile | UnixFileMode.UserExecute;

    private readonly Func<bool> _isRoot;

    public LinuxPlatform()
        : this(() => Environment.IsPrivilegedProcess)
    {
    }

    public LinuxPlatform(Func<bool> isRoot)
    {
        _isRoot = isRoot;
    }

    public string Id => "linux-amd64";

    public string ExecutableName(string name) => name;

    public void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unix file modes are not available on Windows.");
        }

        File.SetUnixFileMode(path, Directory.Exists(path) ? OwnerDirectory : OwnerFile);
    }

    public void EnsureCanRunServer()
    {
        if (_isRoot())
        {
            throw new EmbeddedPostgresException(
                "PostgreSQL cannot run as root. Run the application as a regular user.");
        }
    }
}
