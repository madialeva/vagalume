using Microsoft.Extensions.DependencyInjection;
using Vagalume.Core;
using Vagalume.Host.Desktop;
using Vagalume.Postgres.Embedded;
using Vagalume.Testing;

namespace Vagalume.Host.Desktop.Tests;

[Trait("Category", "Integration")]
public sealed class DesktopHostTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task FirstStart_CreatesClusterServesNotesAndStopsTheDatabase()
    {
        await using var desktop = await RunningDesktop.StartAsync();
        using var client = RunningDesktop.NewClient();

        var landing = await client.GetAsync(desktop.App.StartUrl(desktop.Address), Ct);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, landing.StatusCode);
        var location = Assert.IsType<Uri>(landing.Headers.Location);
        var page = await client.GetAsync(new Uri(new Uri(desktop.Address), location), Ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Notas", await page.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(desktop.DataDirectory, "PG_VERSION")));

        await desktop.StopAsync();

        var database = TestCluster.NewHost(desktop.DataDirectory);
        Assert.False(await database.IsRunningAsync(Ct));
        Assert.False(File.Exists(Path.Combine(desktop.DataDirectory, "postmaster.pid")));
    }

    [Fact]
    public async Task SecondStart_ReusesTheClusterAndKeepsNotes()
    {
        var root = TestEnvironment.NewTempDirectory();
        try
        {
            await using (var first = await RunningDesktop.StartAsync(root))
            {
                await first.App.Web.Services.GetRequiredService<NoteService>().CreateAsync("de la vez anterior", Ct);
            }

            await using var second = await RunningDesktop.StartAsync(root);
            var notes = await second.App.Web.Services.GetRequiredService<NoteService>().ListAsync(Ct);
            Assert.Equal("de la vez anterior", notes.Single().Text);
        }
        finally
        {
            Npgsql.NpgsqlConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ForcedKillOfTheDatabase_IsRecoveredByTheNextLaunchWithoutLosingNotes()
    {
        var root = TestEnvironment.NewTempDirectory();
        try
        {
            await using (var first = await RunningDesktop.StartAsync(root))
            {
                await first.App.Web.Services.GetRequiredService<NoteService>().CreateAsync("confirmada antes del corte", Ct);
                Npgsql.NpgsqlConnection.ClearAllPools();
                var pidFile = Path.Combine(first.DataDirectory, "postmaster.pid");
                using var postmaster = System.Diagnostics.Process.GetProcessById(
                    int.Parse(File.ReadAllLines(pidFile)[0], System.Globalization.CultureInfo.InvariantCulture));
                postmaster.Kill(entireProcessTree: true);
                postmaster.WaitForExit();
                Assert.True(File.Exists(pidFile), "a stale postmaster.pid should remain after the forced kill");
            }

            await using var second = await RunningDesktop.StartAsync(root);

            var notes = await second.App.Web.Services.GetRequiredService<NoteService>().ListAsync(Ct);
            Assert.Equal("confirmada antes del corte", notes.Single().Text);
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
            var app = DesktopWebHost.Create(
                ["--urls=http://127.0.0.1:0"], RunningDesktop.OptionsFor(root), new LinuxPlatform(() => true));
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
}
