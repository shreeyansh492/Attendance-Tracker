using System.Collections.ObjectModel;
using Tracker.Configuration;
using Tracker.Logging;
using Tracker.Machine;
using Tracker.Services;
namespace Tracker.ViewModels
{
    public sealed class DiagnosticsViewModel : ViewModelBase
    {
        private readonly IAppStateService _appStateService;
        private readonly IEmployeeService _employeeService;
        private readonly IMachineIdentityService _machineIdentityService;
        private readonly ApiOptions _apiOptions;
        private readonly ISessionService _sessionService;
        private readonly IEventService _eventService;
        private readonly ILogBufferService _logBufferService;
        private readonly InMemoryLogSink _logSink;
        private DateTimeOffset _lastRefreshedAt = DateTimeOffset.Now;
        private int _pendingOfflineCount;
        private ObservableCollection<string> _logLines = new();


        public DiagnosticsViewModel(
            IAppStateService appStateService,
            IEmployeeService employeeService,
            IMachineIdentityService machineIdentityService,
            ApiOptions apiOptions,
            ISessionService sessionService,
            IEventService eventService,
            ILogBufferService logBufferService,
            InMemoryLogSink logSink)
            
        {
            _appStateService = appStateService;
            _employeeService = employeeService;
            _machineIdentityService = machineIdentityService;
            _apiOptions = apiOptions;
            _sessionService = sessionService;
            _eventService = eventService;
            _logBufferService = logBufferService;
            _logSink = logSink;

            _logSink.LogAdded += OnLogAdded;

            _appStateService.StateChanged += OnStateChanged;
         
        }

        public string AppState =>_appStateService.CurrentState.ToString();
        public string EmployeeName =>_employeeService.Employee?.EmployeeName ?? "Not available";

        public string EmployeeEmail =>_employeeService.Employee?.Email ?? "Not available";
        public string MachineIdentifier =>_machineIdentityService.MachineIdentifier;

        public string MachineName => _machineIdentityService.MachineName;

        public string LastRefreshedAt => _lastRefreshedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm:ss tt");

        public string ApiBaseUrl =>_apiOptions.BaseUrl; public string LastHeartbeat =>_sessionService.CurrentSession?.LastHeartbeatTime?
        .ToLocalTime()
        .ToString("dd MMM yyyy, hh:mm:ss tt")?? "Not available";
        public int PendingOfflineCount
        {
            get => _pendingOfflineCount;
            private set => SetProperty(ref _pendingOfflineCount, value);
        }
        public ObservableCollection<string> LogLines
        {
            get => _logLines;
            private set => SetProperty(ref _logLines, value);
        }
        

        private void OnStateChanged(object? sender,AppStateChangedEventArgs e)
        {
            OnPropertyChanged(nameof(AppState));
            OnPropertyChanged(nameof(EmployeeName));
            OnPropertyChanged(nameof(EmployeeEmail));
            OnPropertyChanged(nameof(LastHeartbeat));
        }
       
        public async Task RefreshPendingOfflineCountAsync( CancellationToken ct = default)
        {
            try
            {
                var events = await _eventService.GetAllPendingEventsAsync(ct);

                PendingOfflineCount = events?.Count ?? 0;
            }
            catch
            {
                PendingOfflineCount = 0;
            }
        }
        public async Task RefreshAsync(
       CancellationToken ct = default)
        {
            try
            {
                ErrorMessage = string.Empty;

                _lastRefreshedAt = DateTimeOffset.Now;
                OnPropertyChanged(nameof(LastRefreshedAt));

                await RefreshPendingOfflineCountAsync(ct);

                LogLines.Clear();

                foreach (var line in _logBufferService.GetLatestLines())
                {
                    LogLines.Add(line);
                }

                OnPropertyChanged(nameof(AppState));
                OnPropertyChanged(nameof(EmployeeName));
                OnPropertyChanged(nameof(EmployeeEmail));
                OnPropertyChanged(nameof(MachineIdentifier));
                OnPropertyChanged(nameof(MachineName));
                OnPropertyChanged(nameof(ApiBaseUrl));
                OnPropertyChanged(nameof(LastHeartbeat));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Diagnostics refresh failed: {ex.Message}";
            }
        }
        public void ClearLogs()
        {
            _logBufferService.Clear();

            LogLines.Clear();
        }
        private void OnLogAdded(object? sender, EventArgs e)
        {
            var latestLines = _logBufferService.GetLatestLines();

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.HasShutdownStarted)
            {
                return;
            }

            // BeginInvoke schedules the work asynchronously without blocking the log sender
            dispatcher.BeginInvoke(() =>
            {
                // Avoid clear-and-rebuild if latestLines is empty
                if (latestLines.Count == 0 && LogLines.Count == 0)
                {
                    return;
                }

                // If your collection supports batch operations or custom ObservableCollection extensions, use them.
                // Otherwise, updating on the UI thread asynchronously prevents background blocking.
                LogLines.Clear();
                foreach (var line in latestLines)
                {
                    LogLines.Add(line);
                }
            });
        }
    }
    
}