using System.IO;
using System.Windows;
using FAT.Data;
using FAT.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FAT.App;

/// <summary>
/// Application entry point and composition root.
///
/// OWNED BY THE TEAM LEAD - see docs/TEAM.md.
/// Module services are registered in FAT.App/Startup/&lt;Module&gt;Registration.cs,
/// one file per member, so that nobody else needs to edit this file.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>Service provider, for the few places that cannot use constructor injection.</summary>
    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
        ?? throw new InvalidOperationException("The host has not been built yet.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                // Per-developer override, never committed. Optional by design.
                config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("FatDatabase")
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings:FatDatabase is missing from appsettings.json.");

                services.AddFatData(connectionString);
                services.AddFatCoreServices();

                // Module registrations go here, one line each:
                //   services.AddAuthModule();
                //   services.AddCatalogAdminModule();
                //   ...
            })
            .Build();

        await _host.StartAsync();

        if (!await CanReachDatabaseAsync())
        {
            Shutdown(1);
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    /// <summary>
    /// Checks the database before showing any window.
    ///
    /// Worth the fifteen minutes it took to write: without it a fresh clone
    /// crashes with a raw SqlException stack trace, and every teammate loses an
    /// hour working out that they simply had not run the setup script yet.
    /// </summary>
    private async Task<bool> CanReachDatabaseAsync()
    {
        try
        {
            using var scope = _host!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FatDbContext>();

            if (await db.Database.CanConnectAsync())
            {
                return true;
            }

            ShowDatabaseError("The FAT database was not found on the configured server.");
            return false;
        }
        catch (Exception ex)
        {
            ShowDatabaseError(ex.Message);
            return false;
        }
    }

    private static void ShowDatabaseError(string detail)
    {
        MessageBox.Show(
            "Cannot connect to the FAT database.\n\n" +
            $"Details: {detail}\n\n" +
            "How to fix:\n" +
            "1. Run db\\setup-db.ps1 from the repository root.\n" +
            "2. If your SQL Server is not the default instance on localhost,\n" +
            $"   create {Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json")}\n" +
            "   and override ConnectionStrings:FatDatabase there.\n" +
            "   (Copy appsettings.Local.json.example as a starting point.)",
            "FAT - Database connection failed",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
