using System;
using System.Collections.Generic;
using System.Text;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Services
{
    public interface IAppStateService
    {
        AppState CurrentState { get; }

        event EventHandler? LoginRequested;
        event EventHandler? LockRequested;
        event EventHandler? UnlockRequested;
        event EventHandler? BreakEndRequested;
        event EventHandler? BreakStartRequested;
        event EventHandler<string>? ForceLoginRequested;

        event EventHandler<AppStateChangedEventArgs>? StateChanged;
        event EventHandler<AutoCheckoutWarningEventArgs>? AutoCheckoutWarningRequested;
        event EventHandler<string>? CriticalMessageRequested;

        Task InitializeAsync(CancellationToken ct = default);

        Task OnIdleThresholdReachedAsync(DateTimeOffset idleStart, CancellationToken ct = default);

        Task OnAutoCheckoutWarningAsync(TimeSpan remainingTime, CancellationToken ct = default);

        Task OnAutoCheckoutAsync(CancellationToken ct = default);

        Task OnIdleEndRequestedAsync(CancellationToken ct = default);

        Task OnManualCheckoutAsync(CancellationToken ct = default);

        Task OnShutdownCheckoutAsync(CancellationToken ct = default);

        Task OnBreakStartRequestedAsync(string reason, CancellationToken ct = default);

        Task OnBreakEndRequestedAsync(CancellationToken ct = default);

        Task ForceReloginAsync(string reason, CancellationToken ct = default);

        Task OnAutoCheckoutWarningAcknowledgedAsync(CancellationToken ct = default);

        public void TransitionTo(AppState state);
    }

    public sealed class AutoCheckoutWarningEventArgs : EventArgs
    {
        public TimeSpan RemainingTime { get; }
        public AutoCheckoutWarningEventArgs(TimeSpan remainingTime)
        {
            RemainingTime = remainingTime;
        }
        

        public int RemainingMinutes => Math.Max(0, (int)Math.Ceiling(RemainingTime.TotalMinutes));
    }

    public sealed class AppStateChangedEventArgs : EventArgs
    {
        public AppStateChangedEventArgs(
        AppState oldState,
        AppState newState)
        {
            OldState = oldState;
            NewState = newState;
        }

        public AppState OldState { get; }

        public AppState NewState { get; }
    }
}
