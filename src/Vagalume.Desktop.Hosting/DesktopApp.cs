using Vagalume.Desktop.Hosting;
namespace Vagalume.Desktop.Hosting;

/// <summary>
/// The desktop web application together with the secret that opens it.
/// </summary>
public sealed class DesktopApp
{
    public DesktopApp(WebApplication web, SessionToken token)
    {
        Web = web;
        Token = token;
    }

    public WebApplication Web { get; }

    public SessionToken Token { get; }

    /// <summary>First address the window must load; it carries the token that is exchanged for a cookie.</summary>
    public string StartUrl(string baseAddress) =>
        $"{baseAddress.TrimEnd('/')}/?{LocalSessionGuard.QueryName}={Token.Value}";
}
