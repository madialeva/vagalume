using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;

namespace Vagalume.Desktop.Hosting;

/// <summary>
/// Middleware that lets only the application window reach the local port: it checks the Host header,
/// then a session cookie, and exchanges the one-time token in the first URL for that cookie.
/// </summary>
public sealed class LocalSessionGuard
{
    public const string CookieName = "vagalume-session";
    public const string QueryName = "vagalume-token";
    private const string LoopbackHost = "127.0.0.1";

    private readonly RequestDelegate _next;
    private readonly SessionToken _token;

    public LocalSessionGuard(RequestDelegate next, SessionToken token)
    {
        _next = next;
        _token = token;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (request.Host.Host != LoopbackHost)
        {
            Reject(context);
            return;
        }

        if (request.Cookies.TryGetValue(CookieName, out var cookie) && _token.Matches(cookie))
        {
            await _next(context);
            return;
        }

        if (HttpMethods.IsGet(request.Method) && request.Query.TryGetValue(QueryName, out var supplied) && _token.Matches(supplied))
        {
            context.Response.Cookies.Append(
                CookieName,
                _token.Value,
                new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true });
            context.Response.Redirect(request.PathBase + request.Path + WithoutToken(request.Query));
            return;
        }

        Reject(context);
    }

    private static void Reject(HttpContext context) => context.Response.StatusCode = StatusCodes.Status403Forbidden;

    private static QueryString WithoutToken(IQueryCollection query)
    {
        var rest = query.Where(pair => pair.Key != QueryName).Select(pair => KeyValuePair.Create(pair.Key, (StringValues)pair.Value));
        return QueryString.Create(rest);
    }
}
