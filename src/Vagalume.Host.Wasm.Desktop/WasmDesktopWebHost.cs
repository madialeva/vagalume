using Microsoft.EntityFrameworkCore;
using Vagalume.Api;
using Vagalume.Core;
using Vagalume.Data;
using Vagalume.Desktop.Hosting;
using Vagalume.Postgres.Embedded;

namespace Vagalume.Host.Wasm.Desktop;

/// <summary>
/// Composition root of the WebAssembly desktop host: it serves the WASM files and the notes API from one local origin,
/// over the embedded PostgreSQL and behind the session token. It knows nothing about Electron so it can be tested without a window.
/// </summary>
public static class WasmDesktopWebHost
{
    public static WebApplicationBuilder CreateBuilder(
        string[] args, DesktopOptions options, SessionToken token, IPlatformStrategy? platform = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(WasmDesktopWebHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });

        // The window address carries the session token, and the request log would write the whole URL.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

        var database = new EmbeddedDatabase();
        builder.Services.AddSingleton(database);
        builder.Services.AddSingleton(token);
        builder.Services.AddSingleton(_ => new EmbeddedPostgresHost(
            new EmbeddedPostgresOptions(options.BinariesDirectory, options.DataDirectory),
            platform ?? PlatformStrategies.Detect(),
            new ProcessRunner(),
            new LoopbackPortFinder(),
            new RandomSecretGenerator()));
        builder.Services.AddSingleton<IDbContextFactory<VagalumeDbContext>>(
            _ => new VagalumeDbContextFactory(database.ConnectionString));
        builder.Services.AddSingleton<INoteRepository, NoteRepository>();
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<NoteService>();
        builder.Services.AddSingleton<DatabaseMigrator>();
        builder.Services.AddHostedService<DesktopStartupService>();
        return builder;
    }

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        var app = builder.Build();
        app.UseMiddleware<LocalSessionGuard>();
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        app.MapNotesApi();
        app.Map("/api/{**rest}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");
        return app;
    }

    /// <summary>Builds the application without Electron, listening where <paramref name="args"/> say.</summary>
    public static DesktopApp Create(
        string[] args,
        DesktopOptions options,
        IPlatformStrategy? platform = null,
        Action<WebApplicationBuilder>? configure = null)
    {
        var token = SessionToken.Generate();
        var builder = CreateBuilder(args, options, token, platform);
        configure?.Invoke(builder);
        return new DesktopApp(Build(builder), token);
    }
}
