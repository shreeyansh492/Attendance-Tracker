using System.Collections.ObjectModel;
using Tracker.Data.Entity;
using Tracker.Services;

namespace Tracker.ViewModels
{
    public sealed class BreakViewModel : ViewModelBase
    {
        private readonly IAppStateService _appStateService;

        public ObservableCollection<BreakReasonOption> Reasons { get; } = new()
        {
            new BreakReasonOption("Tea / Coffee", "TEA_COFFEE"),
            new BreakReasonOption("Lunch", "LUNCH"),
            new BreakReasonOption("Personal", "PERSONAL"),
            new BreakReasonOption("Restroom", "RESTROOM"),
            new BreakReasonOption("Meeting", "MEETING"),
            new BreakReasonOption("Other", "OTHER")
        };

        private string? _selectedReason;
        public string? SelectedReason
        {
            get => _selectedReason;
            set => SetProperty(ref _selectedReason, value);
        }

        public bool CanCloseWindow =>
            _appStateService.CurrentState != AppState.OnBreak;

        public bool IsOnBreak =>
            _appStateService.CurrentState == AppState.OnBreak;

        public BreakViewModel(IAppStateService appStateService)
        {
            _appStateService = appStateService;

            _appStateService.StateChanged += OnStateChanged;

            SelectedReason = Reasons.FirstOrDefault()?.Value;
        }

        public void PrepareForBreak()
        {
            ClearMessage();

            SelectedReason = Reasons.FirstOrDefault()?.Value;
        }

        public async Task<bool> SubmitBreakReasonAsync(
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(SelectedReason))
            {
                SetError("Please select a break type.");
                return false;
            }

            if (_appStateService.CurrentState == AppState.OnBreak)
            {
                SetError("Break is already active.");
                return false;
            }

            try
            {
                IsBusy = true;
                ClearMessage();

                await _appStateService.OnBreakStartRequestedAsync(
                    SelectedReason,
                    ct);

                if (_appStateService.CurrentState != AppState.OnBreak)
                {
                    SetError("Break could not be started.");
                    return false;
                }

                SetStatus("Break started.");
                return true;
            }
            catch (OperationCanceledException)
            {
                SetError("Break start was cancelled.");
                return false;
            }
            catch (Exception ex)
            {
                SetError($"Unable to start break: {ex.Message}");
                return false;
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsOnBreak));
                OnPropertyChanged(nameof(CanCloseWindow));
            }
        }

        public async Task EndBreakAsync(
            CancellationToken ct = default)
        {
            if (_appStateService.CurrentState != AppState.OnBreak)
            {
                SetError("No active break.");
                return;
            }

            try
            {
                IsBusy = true;
                ClearMessage();

                await _appStateService.OnBreakEndRequestedAsync(ct);

                if (_appStateService.CurrentState == AppState.Working)
                {
                    SetStatus("Break ended.");
                    
                }
            }
            catch (OperationCanceledException)
            {
                SetError("Break end was cancelled.");
            }
            catch (Exception ex)
            {
                SetError($"Unable to end break: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsOnBreak));
                OnPropertyChanged(nameof(CanCloseWindow));
            }
        }

        private void OnStateChanged(
            object? sender,
            AppStateChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsOnBreak));
            OnPropertyChanged(nameof(CanCloseWindow));
        }
    }

    public sealed record BreakReasonOption(
        string DisplayName,
        string Value);
}