using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vagalume.Desktop.Hosting;
using Vagalume.Host.Wasm.Desktop;
using Vagalume.Testing;

namespace Vagalume.Host.Wasm.Desktop.Tests;

/// <summary>
/// The WebAssembly desktop web application started on a free loopback port over a throwaway data directory.
/// </summary>
internal sealed class RunningWasmDesktop : IAsyncDisposable
{
    private readonly bool _ownsDirectory;

    private RunningWasmDesktop(DesktopApp app, string root, bool ownsDirectory)
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

    public static async Task<RunningWasmDesktop> StartAsync(string? root = null, Action<WebApplicationBuilder>? configure = null)
    {
        var owns = root is null;
        root ??= TestEnvironment.NewTempDirectory();
        var app = WasmDesktopWebHost.Create(
            ["--urls=http://127.0.0.1:0", "--environment=Development"], OptionsFor(root), configure: configure);
        await app.Web.StartAsync();
        return new RunningWasmDesktop(app, root, owns);
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

    /// <summary>A client that behaves like the window: it follows the token address once and keeps the cookie.</summary>
    public async Task<HttpClient> NewWindowClientAsync()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true }) { BaseAddress = new Uri(Address) };
        (await client.GetAsync(App.StartUrl(Address))).EnsureSuccessStatusCode();
        return client;
    }

    public static HttpClient NewAnonymousClient() => new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
}
