using Microsoft.EntityFrameworkCore;
using Vagalume.Core;
using Vagalume.Data;
using Vagalume.UI;

namespace Vagalume.Host.Web;

/// <summary>
/// Composition root of the web host: wires Core, Data and the shared UI against an external PostgreSQL.
/// </summary>
public static class VagalumeWebHost
{
    public const string ConnectionStringKey = "ConnectionStrings:Vagalume";

    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(VagalumeWebHost).Assembly.GetName().Name,
        });
        var connectionString = builder.Configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new StartupException(
                $"The configuration key '{ConnectionStringKey}' is missing. " +
                "Set it, for example with the environment variable ConnectionStrings__Vagalume.");
        }

        builder.Services.AddSingleton<IDbContextFactory<VagalumeDbContext>>(new VagalumeDbContextFactory(connectionString));
        builder.Services.AddSingleton<INoteRepository, NoteRepository>();
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<NoteService>();
        builder.Services.AddSingleton<DatabaseMigrator>();
        builder.Services.AddHostedService<MigrationHostedService>();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        var app = builder.Build();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }
}
