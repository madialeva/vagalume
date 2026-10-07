namespace Vagalume.Host.Web;

/// <summary>
/// Raised when the web host cannot start for a reason the operator can fix, such as a missing connection string.
/// </summary>
public sealed class StartupException : Exception
{
    public StartupException(string message)
        : base(message)
    {
    }

    public StartupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
