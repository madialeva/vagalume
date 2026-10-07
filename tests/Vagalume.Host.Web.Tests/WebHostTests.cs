using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vagalume.Core;
using Vagalume.Host.Web;
using Vagalume.Testing;

namespace Vagalume.Host.Web.Tests;

[Trait("Category", "Integration")]
public sealed class WebHostTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Startup_WithValidConnection_ServesNotesPageAndPersistsNotes()
    {
        await using var cluster = await StartedClusterAsync();
        await using var app = FactoryFor(cluster.Host.ConnectionString);
        using var client = app.CreateClient();

        var empty = await client.GetAsync("/", Ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, empty.StatusCode);
        Assert.Contains("Notas", await empty.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var script = await client.GetAsync("/_framework/blazor.web.js", Ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, script.StatusCode);
        Assert.True((await script.Content.ReadAsByteArrayAsync(Ct)).Length > 10_000, "the Blazor client script must be served");

        await app.Services.GetRequiredService<NoteService>().CreateAsync("desde el navegador", Ct);

        Assert.Contains("desde el navegador", await client.GetStringAsync("/", Ct), StringComparison.Ordinal);
        NpgsqlConnection.ClearAllPools();
        await using var second = FactoryFor(cluster.Host.ConnectionString);
        var persisted = await second.Services.GetRequiredService<NoteService>().ListAsync(Ct);
        Assert.Equal("desde el navegador", persisted.Single().Text);
    }

    [Fact]
    public void Startup_WithoutConnectionString_FailsNamingTheMissingKey()
    {
        var error = Assert.Throws<StartupException>(() => VagalumeWebHost.Create([]));

        Assert.Contains("ConnectionStrings:Vagalume", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_WithUnreachableDatabase_FailsSayingItCouldNotConnect()
    {
        var app = VagalumeWebHost.Create(
        [
            "--urls=http://127.0.0.1:0",
            "--ConnectionStrings:Vagalume=Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=3",
        ]);
        await using var _ = app;

        var error = await Assert.ThrowsAsync<StartupException>(() => app.StartAsync(Ct));

        Assert.Contains("Could not connect to the database", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoBrowsers_SeeTheSameData()
    {
        await using var cluster = await StartedClusterAsync();
        await using var app = FactoryFor(cluster.Host.ConnectionString);
        using var first = app.CreateClient();
        using var second = app.CreateClient();

        await app.Services.GetRequiredService<NoteService>().CreateAsync("compartida", Ct);

        Assert.Contains("compartida", await first.GetStringAsync("/", Ct), StringComparison.Ordinal);
        Assert.Contains("compartida", await second.GetStringAsync("/", Ct), StringComparison.Ordinal);
    }

    private static async Task<TestCluster> StartedClusterAsync()
    {
        var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        return cluster;
    }

    private static WebApplicationFactory<Program> FactoryFor(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.UseSetting(VagalumeWebHost.ConnectionStringKey, connectionString));
}
