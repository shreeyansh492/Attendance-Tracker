using Tracker.Data.Entity;
using Tracker.Services;

namespace Tracker.ViewModels
{
    public sealed class LockViewModel : ViewModelBase
    {
        private readonly IAppStateService _appStateService;
        private DateTimeOffset? _idleStartedAt;

        public LockViewModel(
            IAppStateService appStateService)
        {
            _appStateService = appStateService;

            _appStateService.StateChanged += OnStateChanged;
        }

        public string Title => "you are currently Idle.";

        public string Subtitle => "Your computer was inactive for the configured period of time.";

        public string IdleStartedAtText => _idleStartedAt.HasValue
                ? $"Idle started at {_idleStartedAt.Value.ToLocalTime():hh:mm:ss tt}"
                : string.Empty;

       

        public void SetIdleStart(DateTimeOffset idleStart)
        {
            _idleStartedAt = idleStart;
            ClearMessage();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Subtitle));
            OnPropertyChanged(nameof(IdleStartedAtText));
            OnPropertyChanged(nameof(StatusMessage));
        }

        private void OnStateChanged(object? sender, AppStateChangedEventArgs e)
        {
            OnPropertyChanged(nameof(StatusMessage));
        }
        public async Task OnContinueWorkingAsync( CancellationToken ct = default)
        {
            if (_appStateService.CurrentState != AppState.IdleLocked )
            {
                SetError("Idle lock is not active.");
                return;
            }

            try
            {
                IsBusy = true;
                ClearMessage();

                await _appStateService.OnIdleEndRequestedAsync(ct);

                if (_appStateService.CurrentState != AppState.Working)
                {
                    SetError("Unable to resume working.");
                    return;
                }

                SetStatus("Welcome back. You are now working.");
            }
            catch (OperationCanceledException)
            {
                SetError("Resume was cancelled.");
            }
            catch (Exception ex)
            {
                SetError($"Unable to resume working: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}