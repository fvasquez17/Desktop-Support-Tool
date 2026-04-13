using System.Windows;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Services;
using DesktopSupportTool.Tray;

namespace DesktopSupportTool;

/// <summary>
/// Application entry point. Initializes the tray icon, enforces single-instance,
/// and manages the application lifecycle.
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private TrayIconManager? _trayManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ─── Single Instance Enforcement ─────────────────────
        const string mutexName = "DesktopSupportTool_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show(
                "Desktop Support Tool is already running.\nCheck the system tray.",
                "Already Running",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ─── Global Exception Handling ───────────────────────
        DispatcherUnhandledException += (s, ex) =>
        {
            LoggingService.Instance.Error("App", "Unhandled UI exception", ex.Exception.ToString());
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            if (ex.ExceptionObject is Exception exception)
                LoggingService.Instance.Error("App", "Unhandled domain exception", exception.ToString());
        };

        TaskScheduler.UnobservedTaskException += (s, ex) =>
        {
            LoggingService.Instance.Error("App", "Unobserved task exception", ex.Exception.ToString());
            ex.SetObserved();
        };

        // ─── Initialize Logging ──────────────────────────────
        LoggingService.Instance.Info("App", "Desktop Support Tool starting...");

        // ─── Register Auto-Start (HKCU — no admin needed) ───
        RegistryHelper.RegisterAutoStart();

        // ─── Create Main Window (hidden — lives in tray) ─────
        var mainWindow = new UI.MainWindow();

        // ─── Initialize System Tray Icon ─────────────────────
        _trayManager = new TrayIconManager(mainWindow);

        // ─── Start Background Health Monitoring ──────────────
        HealthCheckService.Instance.Start(intervalSeconds: 60);

        LoggingService.Instance.Info("App", "Application initialized successfully");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LoggingService.Instance.Info("App", "Application shutting down...");

        // Clean up
        HealthCheckService.Instance.Stop();
        HealthCheckService.Instance.Dispose();
        _trayManager?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
