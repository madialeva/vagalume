namespace Vagalume.Host.Desktop;

/// <summary>
/// Holds the connection string of the embedded server once it has started, for components created afterwards.
/// </summary>
public sealed class EmbeddedDatabase
{
    private string? _connectionString;

    public string ConnectionString
    {
        get => _connectionString ?? throw new InvalidOperationException("The embedded database has not started.");
        set => _connectionString = value;
    }
}
