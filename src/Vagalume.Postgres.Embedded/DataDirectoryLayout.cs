namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Knows the files the host keeps inside a cluster directory next to the ones PostgreSQL owns.
/// </summary>
public sealed class DataDirectoryLayout
{
    public DataDirectoryLayout(string dataDirectory)
    {
        Root = dataDirectory;
    }

    public string Root { get; }

    public string VersionFile => Path.Combine(Root, "PG_VERSION");

    public string SecretFile => Path.Combine(Root, "vagalume.secret");

    public string PortFile => Path.Combine(Root, "vagalume.port");

    public string HostConfigFile => Path.Combine(Root, "vagalume.conf");

    public string LogFile => Path.Combine(Root, "vagalume-server.log");

    public string PidFile => Path.Combine(Root, "postmaster.pid");

    public bool IsInitialized => File.Exists(VersionFile);

    public bool IsEmpty => !Directory.Exists(Root) || !Directory.EnumerateFileSystemEntries(Root).Any();

    public string ReadSecret() => File.ReadAllText(SecretFile).Trim();

    public int ReadPort() => int.Parse(File.ReadAllText(PortFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);
}
