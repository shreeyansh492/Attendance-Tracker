using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Windows;
using Tracker.Bootstrap;
using Tracker.Configuration;
using Tracker.Data;
using Tracker.Logging;
using Tracker.Services;
using Tracker.Tray;
using Tracker.View;




namespace Tracker;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private Mutex? _singleInstanceMutex;
    private bool _isHandlingSessionEnd;

    protected override async void OnStartup(
        StartupEventArgs e)
    {
        base.OnStartup(e);
        SessionEnding += App_SessionEnding;

        _singleInstanceMutex = new Mutex(
    true,
    "Tracker.SingleInstance",
    out var isFirstInstance);

        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show(
                "Tracker is already running. Please check the system tray.",
                "Tracker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        try
        {
            _host = BuildHost(e.Args);

            await _host.StartAsync();
            await EnsureDatabaseAsync();

            InitializeTrayIcon();

            await InitializeAppStateAsync();
            Log.Information(
                "Tracker started successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(
                ex,
                "WizeVision Attendance Agent failed to start.");

            System.Windows.MessageBox.Show(
                ex.ToString(),
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }
  

    private async void App_SessionEnding(
        object sender,
        SessionEndingCancelEventArgs e)
    {
        // Prevent this from running more than once.
        if (_isHandlingSessionEnd)
            return;

        _isHandlingSessionEnd = true;

        // Pause Windows shutdown temporarily so checkout can be sent.
        e.Cancel = true;

        try
        {
            if (_host is null)
            {
                Shutdown();
                return;
            }

            var appStateService = _host.Services
                .GetRequiredService<IAppStateService>();

            Log.Information(
                "Windows session ending detected. Attempting shutdown checkout.");

            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

            await appStateService.OnShutdownCheckoutAsync(
                cts.Token);

            Log.Information(
                "Shutdown checkout handling completed.");
        }
        catch (Exception ex)
        {
            // Never prevent the user from shutting down because of Tracker.
            Log.Error(
                ex,
                "Shutdown checkout failed. Continuing Windows shutdown.");
        }
        finally
        {
            Shutdown();
        }
    }

    protected override async void OnExit(
     ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                var loginWindow = _host.Services.GetService<LoginWindow>();
                var lockWindow = _host.Services.GetService<LockWindow>();
               
                var warningWindow = _host.Services.GetService<AutoCheckoutWarningWindow>();
                var trayStatusWindow = _host.Services.GetService<TrayStatusWindow>();
                var diagnosticsWindow = _host.Services.GetService<DiagnosticsWindow>();
                var trayService = _host.Services.GetService<ITrayService>();
                var screenLockService = _host.Services.GetService<IScreenLockService>();

                loginWindow?.AllowCloseForShutdown();
                lockWindow?.AllowCloseForShutdown();
                warningWindow?.AllowCloseForShutdown();
                trayStatusWindow?.AllowCloseForShutdown();
                diagnosticsWindow?.AllowCloseForShutdown();


                trayService?.Dispose();
                screenLockService?.Dispose();

                await _host.StopAsync(
                    TimeSpan.FromSeconds(5));

                _host.Dispose();
            }
        }
        catch
        {
            // App is shutting down. Avoid blocking shutdown because of cleanup failure.
        }
        finally
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
            catch
            {
                // Ignore mutex cleanup errors during shutdown.
            }

            Log.CloseAndFlush();

            base.OnExit(e);
        }
    }

    private static IHost BuildHost(
        string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);

                config.AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: true);

                config.AddEnvironmentVariables(
                    prefix: "TRACKER_");
            })
            .ConfigureServices((context, services) =>
            {
                services.AddTrackerAgent(
                    context.Configuration);
            })
            .UseSerilog((context, services, loggerConfiguration) =>
            {
                var logPaths = services.GetRequiredService<LogPaths>();
                var inMemorySink = services.GetRequiredService<InMemoryLogSink>();
                var appOptions = services.GetRequiredService<AppOptions>();

                loggerConfiguration
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override(
                        "Microsoft",
                        LogEventLevel.Information)
                    .MinimumLevel.Override(
                        "System.Net.Http.HttpClient",
                        LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty(
                        "Application",
                        appOptions.Name)
                    .WriteTo.File(
                        path: logPaths.CurrentLogFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: null,
                        shared: true,
                        restrictedToMinimumLevel: LogEventLevel.Debug,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Sink(
                        inMemorySink,
                        restrictedToMinimumLevel: LogEventLevel.Debug);
            })
            .Build();
    }

    private async Task EnsureDatabaseAsync()
    {
        if (_host is null)
            throw new InvalidOperationException("Application host was not created.");

        using var scope = _host.Services.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IAppRepository>();

        await repository.EnsureDatabaseAsync();
    }

    private async Task InitializeAppStateAsync()
    {
        if (_host is null)
            throw new InvalidOperationException("Application host was not created.");

        var appStateService = _host.Services
            .GetRequiredService<IAppStateService>();

        var sessionService = _host.Services
            .GetRequiredService<ISessionService>();

        var screenLockService = _host.Services
     .GetRequiredService<IScreenLockService>();

        appStateService.LoginRequested += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                screenLockService.ShowLogin();
            });
        };

        appStateService.LockRequested += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                var idleStartedUtc = DateTimeOffset.UtcNow;

                var currentIdleStartUtc = sessionService
                    .CurrentSession?
                    .CurrentIdleStartUtc;

                if (currentIdleStartUtc.HasValue)
                {
                    idleStartedUtc = currentIdleStartUtc.Value.ToUniversalTime();
                }

                screenLockService.ShowIdleLock(
                    idleStartedUtc);
            });
        };

        appStateService.UnlockRequested += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                screenLockService.Unlock();
            });
        };



        appStateService.AutoCheckoutWarningRequested += (_, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                screenLockService.ShowAutoCheckoutWarning(
                    args.RemainingTime);
            });
        };

        appStateService.CriticalMessageRequested += (_, message) =>
        {
            Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(
                    message,
                    "Tracker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        };

        appStateService.BreakStartRequested += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                screenLockService.ShowBreak();
            });
        };

        appStateService.BreakEndRequested += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                screenLockService.Unlock();
            });
        };

        appStateService.ForceLoginRequested += (_, reason) =>
        {
            Dispatcher.Invoke(() =>
            {
                /*
                 * Important:
                 * Do not show blocking MessageBox here.
                 * ScreenLockService hides warning/idle/resume windows
                 * and shows login on all monitors.
                 */
                screenLockService.ShowLogin(
                    reason);
            });
        };

        await appStateService.InitializeAsync();
    }

    private void InitializeTrayIcon()
    {
        if (_host is null)
            throw new InvalidOperationException("Application host was not created.");

        var trayService = _host.Services
            .GetRequiredService<ITrayService>();

        trayService.Initialize();
    }
}