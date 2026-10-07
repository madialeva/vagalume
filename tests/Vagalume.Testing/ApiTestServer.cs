using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vagalume.Api;
using Vagalume.Core;
using Vagalume.Data;

namespace Vagalume.Testing;

/// <summary>
/// A real Kestrel server on a free loopback port that serves only the notes API, over a given PostgreSQL
/// or over a repository supplied by the test.
/// </summary>
public sealed class ApiTestServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ApiTestServer(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public static async Task<ApiTestServer> StartAsync(string connectionString, INoteRepository? repository = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ApiTestServer).Assembly.GetName().Name });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var factory = new VagalumeDbContextFactory(connectionString);
        builder.Services.AddSingleton<IDbContextFactory<VagalumeDbContext>>(factory);
        builder.Services.AddSingleton<INoteRepository>(repository ?? new NoteRepository(factory));
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<NoteService>();

        var app = builder.Build();
        app.MapNotesApi();
        if (repository is null)
        {
            await new DatabaseMigrator(factory).MigrateAsync(CancellationToken.None);
        }

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new ApiTestServer(app, new Uri(address));
    }

    public HttpClient NewClient() => new() { BaseAddress = BaseAddress };

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
