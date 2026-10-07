namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Strategy that isolates everything that differs between Linux and Windows: binary names,
/// file permissions and the checks needed before a server can run.
/// </summary>
public interface IPlatformStrategy
{
    /// <summary>Platform identifier used in the binaries lock file, for example <c>linux-amd64</c>.</summary>
    string Id { get; }

    /// <summary>Returns the file name of an executable, adding the platform suffix when it has one.</summary>
    string ExecutableName(string name);

    /// <summary>Makes a file or directory readable only by its owner, when the platform can express that.</summary>
    void RestrictToOwner(string path);

    /// <summary>Throws when the current process is not allowed to run the PostgreSQL server.</summary>
    void EnsureCanRunServer();
}
