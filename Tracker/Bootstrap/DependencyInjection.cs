using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Api;
using Tracker.Configuration;
using Tracker.Data;
using Tracker.Logging;
using Tracker.Machine;
using Tracker.Security;
using Tracker.Services;
using Tracker.Tray;
using Tracker.View;
using Tracker.ViewModels;
using Tracker.Workers;

namespace Tracker.Bootstrap
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddTrackerAgent(this IServiceCollection services, IConfiguration configuration)
        {
            var appOptions = BindRequired<AppOptions>(configuration, AppOptions.SectionName);
            var apiOptions = BindRequired<ApiOptions>(configuration, ApiOptions.SectionName);
            apiOptions.Validate();
            var attendanceOptions = BindRequired<AttendanceOptions>(configuration, AttendanceOptions.SectionName);
            attendanceOptions.Validate();
            var storageOptions = BindRequired<StorageOptions>(configuration, StorageOptions.SectionName);
            storageOptions.Validate();
            var diagnosticsOptions = BindRequired<DiagnosticsOptions>(configuration, DiagnosticsOptions.SectionName);
            diagnosticsOptions.Validate();

            services.AddSingleton(appOptions);
            services.AddSingleton(apiOptions);
            services.AddSingleton(attendanceOptions);
            services.AddSingleton(storageOptions);
            services.AddSingleton(diagnosticsOptions);

            services.AddSingleton<LogPaths>();
            services.AddSingleton<InMemoryLogSink>();
            services.AddSingleton<ILogBufferService>(
                provider => provider.GetRequiredService<InMemoryLogSink>());
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseSqlite(storageOptions.GetSqliteConnectionString());
            });

            services.AddSingleton<IAppRepository, AppRepository>();
            services.AddSingleton<ISecureTokenStore, DpapiSecureTokenStore>();
            services.AddSingleton<IMachineIdentityService, MachineIdentityService>();
            services.AddSingleton<IIdleDetectionService, IdleDetectionService>();
            services.AddSingleton<IClockService, ClockService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IEventService, EventService>();
            services.AddSingleton<IEmployeeService, EmployeeService>();
            services.AddSingleton<IAppStateService, AppStateService>();
            services.AddSingleton<IScreenLockService, ScreenLockService>();
            services.AddSingleton<ITrayService, TrayService>();
            services.AddSingleton<IAuthService, AuthService>();



            services.AddSingleton<LoginViewModel>();
            services.AddSingleton<LockViewModel>();
            services.AddSingleton<BreakViewModel>();
            services.AddSingleton<AutoCheckoutWarningViewModel>();
            services.AddSingleton<TrayStatusViewModel>();
            services.AddSingleton<DiagnosticsViewModel>();

            // Windows
            services.AddSingleton<LoginWindow>();
            services.AddSingleton<LockWindow>();
            services.AddSingleton<BreakWindow>();
            services.AddSingleton<AutoCheckoutWarningWindow>();
            services.AddSingleton<TrayStatusWindow>();
            services.AddSingleton<DiagnosticsWindow>();

            services.AddHttpClient<IApiClient, ApiClient>();

            services.AddHostedService<HeartbeatWorker>();
            services.AddHostedService<IdleMonitorWorker>();
            services.AddHostedService<CleanupWorker>();


            return services;
        }

        private static T BindRequired<T>(IConfiguration configuration, string sectionName) where T : class, new()
        {
            var section = configuration.GetSection(sectionName);

            if (!section.Exists())
            {
                throw new InvalidOperationException($"Configuration section '{sectionName}' is missing in appsettings.json");
            }
            var value = section.Get<T>();

            if (value is null)
            {
                throw new InvalidOperationException($"Configuration section '{sectionName}' could not be found.");
            }

            return value;
        }
    }
}
