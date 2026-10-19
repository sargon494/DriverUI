using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Ui.Wpf.Services;
using Ui.Wpf.ViewModels;
using Ui.Wpf.Views;

namespace Ui.Wpf;

/// <summary>
/// Punto de entrada de la aplicación WPF.
/// Configura el contenedor DI, Serilog y abre la ventana Shell.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Serilog ──────────────────────────────────────────────
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DriverUpdater", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDir, "ui-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        // ── DI container ─────────────────────────────────────────
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(b => b.AddSerilog(dispose: true).SetMinimumLevel(LogLevel.Debug));

        // gRPC service wrapper
        services.AddSingleton<DriverAgentService>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ShellViewModel>();

        // Shell window
        services.AddTransient<ShellWindow>();

        _services = services.BuildServiceProvider();

        // ── Abrir ventana ─────────────────────────────────────────
        var shell = _services.GetRequiredService<ShellWindow>();
        shell.Show();

        // Iniciar conexión en background
        var shellVm = _services.GetRequiredService<ShellViewModel>();
        await shellVm.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<ShellViewModel>()?.Shutdown();
        _services?.GetService<DriverAgentService>()?.Disconnect();
        Log.CloseAndFlush();
        _services?.Dispose();
        base.OnExit(e);
    }
}
