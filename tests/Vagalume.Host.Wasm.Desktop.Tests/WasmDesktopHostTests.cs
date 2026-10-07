using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vagalume.Api.Client;
using Vagalume.Api.Contracts;
using Vagalume.Core;
using Vagalume.Desktop.Hosting;
using Vagalume.Host.Wasm.Desktop;
using Vagalume.Postgres.Embedded;
using Vagalume.Testing;

namespace Vagalume.Host.Wasm.Desktop.Tests;

[Trait("Category", "Integration")]
public sealed class WasmDesktopHostTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task WithoutASession_EverythingIsRejectedWithoutContent()
    {
        await using var desktop = await RunningWasmDesktop.StartAsync();
        using var client = RunningWasmDesktop.NewAnonymousClient();

        foreach (var path in new[] { "/", "/index.html", ApiRoutes.Notes, "/_framework/blazor.webassembly.js", "/anything" })
        {
            var response = await client.GetAsync(new Uri(new Uri(desktop.Address), path), Ct);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync(Ct));
        }

        var post = await client.PostAsJsonAsync(new Uri(new Uri(desktop.Address), ApiRoutes.Notes), new CreateNoteRequest("x"), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Empty(await desktop.App.Web.Services.GetRequiredService<NoteService>().ListAsync(Ct));
    }

    [Fact]
    public async Task WithTheWindowSession_ServesTheWasmApplicationAndItsBootFiles()
    {
        await using var desktop = await RunningWasmDesktop.StartAsync();
        using var window = await desktop.NewWindowClientAsync();

        var html = await window.GetStringAsync("/", Ct);
        Assert.Contains("<div id=\"app\">", html, StringComparison.Ordinal);
        Assert.Contains("_framework/blazor.webassembly.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("#[", html, StringComparison.Ordinal);
        const string script = "/_framework/blazor.webassembly.js";

        var boot = await window.GetAsync(script, Ct);
        Assert.Equal(HttpStatusCode.OK, boot.StatusCode);
        Assert.True((await boot.Content.ReadAsByteArrayAsync(Ct)).Length > 10_000, "the Blazor WebAssembly loader must be served");
    }

    [Fact]
    public async Task TheWasmClient_UsesTheApiThroughTheSessionCookie()
    {
        await using var desktop = await RunningWasmDesktop.StartAsync();
        using var window = await desktop.NewWindowClientAsync();
        var api = new NotesApiClient(window);

        var created = await api.CreateAsync("desde el cliente", Ct);
        var listed = await api.ListAsync(Ct);

        Assert.Equal("desde el cliente", created.Text);
        Assert.Equal(created.Id, Assert.Single(listed).Id);
        await Assert.ThrowsAsync<ApiValidationException>(() => api.CreateAsync(" ", Ct));
    }

    [Fact]
    public async Task UnknownApiPaths_Answer404InsteadOfTheApplicationPage()
    {
        await using var desktop = await RunningWasmDesktop.StartAsync();
        using var window = await desktop.NewWindowClientAsync();

        var response = await window.GetAsync("/api/nothing-here", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Notes_SurviveARestartAndTheDatabaseStopsWithTheHost()
    {
        var root = TestEnvironment.NewTempDirectory();
        try
        {
            await using (var first = await RunningWasmDesktop.StartAsync(root))
            {
                await first.App.Web.Services.GetRequiredService<NoteService>().CreateAsync("persistente", Ct);
            }

            Assert.False(await TestCluster.NewHost(Path.Combine(root, "data")).IsRunningAsync(Ct));
            await using var second = await RunningWasmDesktop.StartAsync(root);
            using var window = await second.NewWindowClientAsync();

            var listed = await new NotesApiClient(window).ListAsync(Ct);

            Assert.Equal("persistente", Assert.Single(listed).Text);
        }
        finally
        {
            Npgsql.NpgsqlConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Root_IsRejectedBeforeStartingTheDatabase()
    {
        var root = TestEnvironment.NewTempDirectory();
        try
        {
            var app = WasmDesktopWebHost.Create(
                ["--urls=http://127.0.0.1:0"], RunningWasmDesktop.OptionsFor(root), new LinuxPlatform(() => true));
            await using var _ = app.Web;

            var error = await Assert.ThrowsAsync<EmbeddedPostgresException>(() => app.Web.StartAsync(Ct));

            Assert.Contains("root", error.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(root, "data")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Token_IsNeverWrittenToTheLogs()
    {
        var logs = new CollectingLoggerProvider();
        await using var desktop = await RunningWasmDesktop.StartAsync(configure: builder =>
        {
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Logging.AddProvider(logs);
        });
        using var window = await desktop.NewWindowClientAsync();
        await window.GetAsync(ApiRoutes.Notes, Ct);

        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, m => m.Contains(desktop.App.Token.Value, StringComparison.Ordinal));
    }

}
