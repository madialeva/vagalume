using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Vagalume.Postgres.Embedded;

namespace Vagalume.Host.Desktop;

/// <summary>
/// Entry point of the desktop host: joins the testable web application with an Electron.NET window.
/// </summary>
public static class DesktopProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        var token = SessionToken.Generate();
        WebApplication? web = null;
        var builder = DesktopWebHost.CreateBuilder(args, DesktopOptions.ForCurrentUser(), token);
        builder.UseElectron(args, async () =>
        {
            var address = web!.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            var options = new BrowserWindowOptions { Show = false, Width = 1100, Height = 750, IsRunningBlazor = true };
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                options.AutoHideMenuBar = true;
            }

            var window = await Electron.WindowManager.CreateWindowAsync(options, new DesktopApp(web, token).StartUrl(address));
            window.OnReadyToShow += () => window.Show();
        });

        try
        {
            web = DesktopWebHost.Build(builder);
            await web.RunAsync();
            return 0;
        }
        catch (EmbeddedPostgresException error)
        {
            await Console.Error.WriteLineAsync(error.Message);
            return 1;
        }
    }
}
