namespace Vagalume.Host.Desktop;

/// <summary>
/// Where the desktop host keeps the user's cluster and finds the PostgreSQL distribution that ships with the application.
/// </summary>
public sealed record DesktopOptions(string DataDirectory, string BinariesDirectory)
{
    /// <summary>Per-user data directory and the <c>postgres</c> directory next to the executable.</summary>
    public static DesktopOptions ForCurrentUser() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vagalume", "data"),
        Path.Combine(AppContext.BaseDirectory, "postgres"));
}
