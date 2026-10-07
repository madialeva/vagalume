namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Error raised when the embedded PostgreSQL server cannot be prepared, started or managed.
/// </summary>
public sealed class EmbeddedPostgresException : Exception
{
    public EmbeddedPostgresException(string message)
        : base(message)
    {
    }

    public EmbeddedPostgresException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
