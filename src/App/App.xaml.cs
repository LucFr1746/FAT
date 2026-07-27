using System.IO;
using System.Windows;
using App.Startup;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Services;

namespace App;

/// <summary>
/// Application entry point and composition root.
///
/// OWNED BY THE CVerify team - see docs/TEAM.md.
/// Module services are registered in App/Startup/&lt;Module&gt;Registration.cs,
/// one file per member, so that nobody else needs to edit this file.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    [STAThread]
    public static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>Service provider, for the few places that cannot use constructor injection.</summary>
    public static IServiceProvider Services =>
        ((global::App.App)Current)._host?.Services
        ?? throw new InvalidOperationException("The host has not been built yet.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [UNHANDLED DOMAIN EXCEPTION] {ex}\n";
            try { File.AppendAllText("app_crash.log", logMsg); } catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            var ex = args.Exception;
            var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [DISPATCHER UNHANDLED EXCEPTION] {ex}\n";
            try { File.AppendAllText("app_crash.log", logMsg); } catch { }
            args.Handled = true; // Prevent process termination without re-entrant dialog loop!
        };

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
                var connectionString = context.Configuration.GetConnectionString("AppDatabase")
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings:AppDatabase is missing from appsettings.json.");

                services.AddAppData(connectionString);
                services.AddCoreServices();

                // One line per module, per docs/TEAM.md: the module owns its own
                // registration file so nobody else has to edit this one.
                services.AddCatalogModule();
                services.AddMaterialsModule();
                services.AddGradeModule();

                // Navigation & Container ViewModels
                services.AddSingleton<Navigation.INavigationService, Navigation.NavigationService>();
                services.AddSingleton<ViewModels.MainWindowViewModel>();
                services.AddTransient<ViewModels.DashboardViewModel>();

                // Auth ViewModels
                services.AddTransient<ViewModels.Auth.LoginViewModel>();
                services.AddTransient<ViewModels.Auth.RegisterViewModel>();
                services.AddTransient<ViewModels.Auth.AcademicProfileSetupViewModel>();
                services.AddTransient<ViewModels.Auth.ProfileViewModel>();
                services.AddTransient<ViewModels.Auth.ChangePasswordViewModel>();
                services.AddTransient<ViewModels.Auth.UserManagementViewModel>();
            })
            .Build();

        await _host.StartAsync();

        if (!await CanReachDatabaseAsync())
        {
            Shutdown(1);
            return;
        }

        var navService = _host.Services.GetRequiredService<Navigation.INavigationService>();
        var mainViewModel = _host.Services.GetRequiredService<ViewModels.MainWindowViewModel>();

        // Initial navigation to LoginView
        await navService.NavigateToAsync<ViewModels.Auth.LoginViewModel>();

        var main = new MainWindow
        {
            DataContext = mainViewModel
        };
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
            var db = scope.ServiceProvider.GetRequiredService<FAT_DBContext>();

            if (await db.Database.CanConnectAsync())
            {
                // Applies any schema change a teammate's database has not seen
                // yet. Idempotent and never destructive - see SchemaUpgrader.
                db.EnsureDatabaseSchemaUpToDate();

                // Automatically seeds curriculum data from db/data/csv if DB is unseeded
                await DataSeeder.SeedCurriculumIfEmptyAsync(db);

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
            "   and override ConnectionStrings:AppDatabase there.\n" +
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
