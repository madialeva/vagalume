using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vagalume.Host.Desktop;
using Vagalume.Postgres.Embedded;
using Vagalume.Testing;

namespace Vagalume.Host.Desktop.Tests;

/// <summary>
/// A desktop web application started on a free loopback port over a throwaway data directory.
/// </summary>
internal sealed class RunningDesktop : IAsyncDisposable
{
    private readonly bool _ownsDirectory;

    private RunningDesktop(DesktopApp app, string root, bool ownsDirectory)
    {
        App = app;
        Root = root;
        _ownsDirectory = ownsDirectory;
    }

    public DesktopApp App { get; }

    public string Root { get; }

    public string DataDirectory => Path.Combine(Root, "data");

    public string Address =>
        App.Web.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    public static DesktopOptions OptionsFor(string root) => new(Path.Combine(root, "data"), TestEnvironment.BinariesDirectory);

    public static async Task<RunningDesktop> StartAsync(string? root = null, Action<WebApplicationBuilder>? configure = null)
    {
        var owns = root is null;
        root ??= TestEnvironment.NewTempDirectory();
        var app = DesktopWebHost.Create(["--urls=http://127.0.0.1:0", "--environment=Development"], OptionsFor(root), configure: configure);
        await app.Web.StartAsync();
        return new RunningDesktop(app, root, owns);
    }

    public async Task StopAsync()
    {
        await App.Web.StopAsync();
        await App.Web.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (ObjectDisposedException)
        {
        }

        NpgsqlConnection.ClearAllPools();
        if (_ownsDirectory)
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    public static HttpClient NewClient(bool useCookies = true) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = useCookies,
    });
}
