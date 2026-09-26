using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Api;
using Tracker.Configuration;
using Tracker.Core;
using Tracker.Data;
using Tracker.Data.Entity;
using Tracker.Dto;
using Tracker.Security;

namespace Tracker.Services
{
    public sealed class AppStateService : IAppStateService
    {
        private readonly IApiClient _apiClient;
        private readonly ISessionService _sessionService;
        private readonly IClockService _clockService;
        private readonly ISecureTokenStore _tokenStore;
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<AppStateService> _logger;
        private readonly AttendanceOptions _attendanceOptions;
        private readonly IEventService _eventService;
        private readonly IAppRepository _repository;

        private readonly SemaphoreSlim _gate = new(1, 1);

        public AppStateService(
            IApiClient apiClient,
            ISessionService sessionService,
            IEmployeeService employeeService,
            IClockService clockService,
            ISecureTokenStore tokenStore,
            ILogger<AppStateService> logger,
            AttendanceOptions attendanceOptions,
            IEventService eventService,
            IAppRepository repository)
        {
            _apiClient = apiClient;
            _sessionService = sessionService;
            _employeeService = employeeService;
            _clockService = clockService;
            _tokenStore = tokenStore;
            _logger = logger;
            _attendanceOptions = attendanceOptions;
            _eventService = eventService;
            _repository = repository;
        }

        public AppState CurrentState { get; private set; } = AppState.Starting;

        public bool IsWorking => CurrentState == AppState.Working;
        public bool IsIdleLocked => CurrentState == AppState.IdleLocked;
        public bool IsLoggedOut => CurrentState == AppState.LoggedOut;
        public bool IsOnBreak => CurrentState == AppState.OnBreak;

        public event EventHandler<AppStateChangedEventArgs>? StateChanged;
        public event EventHandler? LoginRequested;
        public event EventHandler? LockRequested;
        public event EventHandler? UnlockRequested;
        public event EventHandler? BreakStartRequested;
        public event EventHandler? BreakEndRequested;
        public event EventHandler<AutoCheckoutWarningEventArgs>? AutoCheckoutWarningRequested;
        public event EventHandler<string>? ForceLoginRequested;
        public event EventHandler<string>? CriticalMessageRequested;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                _logger.LogInformation("Initializing app state.");

                await _sessionService.ClearCurrentSessionAsync();
                _tokenStore.ClearAccessToken();

                TransitionTo(AppState.LoggedOut);

                LoginRequested?.Invoke(this, EventArgs.Empty);
                _logger.LogInformation("Login screen requested.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AppState initialization failed.");
                TransitionTo(AppState.Error);
                CriticalMessageRequested?.Invoke(this, "Tracker failed to initialize AppState.");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task OnManualCheckoutAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                if (CurrentState == AppState.LoggedOut || CurrentState == AppState.CheckedOut)
                {
                    return;
                }

                var currentSession = _sessionService.CurrentSession;
                if (currentSession is null)
                {
                    _logger.LogWarning("Cannot perform checkout. No active session found");
                    return;
                }

                var checkoutTime = _clockService.UtcNow;
                var employee = await _employeeService.GetEmployeeBySessionIdAsync(currentSession.Id, ct);
                var email = employee?.Email ?? _employeeService.Employee?.Email ?? "system";

                _logger.LogInformation("Manual checkout requested. CheckoutTime = {time}", checkoutTime);

                var checkoutEvent = new EventsEntity
                {
                    Id = Guid.NewGuid(),
                    LocalSessionId = currentSession.Id,
                    EventTime = _clockService.ToUtcIso(checkoutTime),
                    EventType = EventTypes.CHECK_OUT,
                    IsSynced = false,
                    Version = 0,
                    CreatedAt = checkoutTime,
                    UpdatedAt = checkoutTime,
                    CreatedBy = email,
                    MetaData= "Manual checkout.",
                    UpdatedBy = email
                };

                // 1. Save locally first
                await _repository.SaveEventAsync(checkoutEvent, ct);

                try
                {
                    // 2. Fetch all pending events (includes the checkout event)
                    var pendingRequests = await _eventService.GetAllPendingEventsAsync(ct);

                    if (pendingRequests is not null && pendingRequests.Count > 0)
                    {
                        // Ensure our checkout event is definitely in the payload
                        if (!pendingRequests.Any(x => x.EventId == checkoutEvent.Id))
                        {
                            pendingRequests.Add(new AttendanceEventRequest
                            {
                                SessionId = currentSession.Id,
                                EventId = checkoutEvent.Id,
                                EmployeeId = currentSession.EmployeeId,
                                EventTime = checkoutEvent.EventTime,
                                EventType = checkoutEvent.EventType,
                                MetaData = checkoutEvent.MetaData,
                                IsOffline = checkoutEvent.Version == 0
                            });
                        }

                        // 3. Send to backend API
                        await _apiClient.SendAttendanceEventAsync(pendingRequests, ct);

                        // 4. Mark all sent events as successfully synced
                        foreach (var request in pendingRequests)
                        {
                            await _eventService.UpdateAsync(request.EventId, ct);
                        }
                    }
                }
                catch (SessionExpiredException ex)
                {
                    _logger.LogWarning(ex, "CHECK_OUT failed because session expired.");
                    await ForceReloginInternalAsync("Session expired. Please login again.", ct);
                    return;
                }
                catch (ApiRequestException ex) when (ex.IsBusinessError)
                {
                    _logger.LogWarning(ex, "CHECK_OUT rejected by backend. Status={Status}, Body={Body}", (int)ex.StatusCode, ex.ResponseBody);
                    await ForceReloginInternalAsync("Checkout rejected by server. Please login again.", ct);
                    return;
                }
                catch (Exception ex) when (IsNetworkLikeFailure(ex))
                {
                    _logger.LogWarning(ex, "CHECK_OUT could not reach backend. Queued offline.");
                }

                await _sessionService.MarkCheckedoutAsync(currentSession.Id, checkoutTime, ct);
                TransitionTo(AppState.CheckedOut);

                await ForceReloginInternalAsync("Checked out successfully.", ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task ForceReloginInternalAsync(string reason, CancellationToken ct = default)
        {
            _logger.LogWarning("Force re-login requested. Reason={reason}", reason);

            await _sessionService.ClearCurrentSessionAsync();
            _tokenStore.ClearAccessToken();
            TransitionTo(AppState.LoggedOut);

            ForceLoginRequested?.Invoke(this, reason);

            // FIX: Fire the standard LoginRequested event as a fallback to guarantee the Lock Screen appears 
            // even if the bootstrapper doesn't listen to ForceLoginRequested.
            LoginRequested?.Invoke(this, EventArgs.Empty);
        }

        public async Task OnAutoCheckoutAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (CurrentState != AppState.IdleLocked)
                {
                    _logger.LogInformation("Auto-Checkout Skipped because current state is {state}", CurrentState);
                    return;
                }

                TransitionTo(AppState.CheckedOut);

                var currentSession = _sessionService.CurrentSession;
                if (currentSession is null)
                {
                    _logger.LogWarning("No current session found for auto-checkout.");
                    throw new InvalidOperationException("Current session is null");
                }

                var checkoutTime = _clockService.UtcNow;
                _logger.LogInformation("Auto-Checkout triggered. checkout time = {time}", _clockService.ToUtcIso(checkoutTime));

                bool alreadyHandledForceLogin = false;

                try
                {
                    var employee = await _employeeService.GetEmployeeBySessionIdAsync(currentSession.Id, ct);
                    var email = employee?.Email ?? _employeeService.Employee?.Email ?? "system";

                    var checkoutEvent = new EventsEntity
                    {
                        Id = Guid.NewGuid(),
                        LocalSessionId = currentSession.Id,
                        EventTime = _clockService.ToUtcIso(checkoutTime),
                        EventType = EventTypes.CHECK_OUT,
                        IsSynced = false,
                        MetaData = "Auto-Checkout.",
                        Version = 0,
                        CreatedAt = checkoutTime,
                        UpdatedAt = checkoutTime,
                        UpdatedBy = email,
                        CreatedBy = email
                    };

                    try
                    {
                        await _repository.SaveEventAsync(checkoutEvent, ct);
                    }
                    catch (Exception ex)
                    {
                        // Fixed LOW-6: Fixed incorrect log message referencing checkin
                        _logger.LogError(ex, "Error occurred while saving checkout event {EventId} to database.", checkoutEvent.Id);
                        throw;
                    }

                    var pendingRequests = await _eventService.GetAllPendingEventsAsync(ct);
                    if (pendingRequests != null && pendingRequests.Count > 0)
                    {
                        await _apiClient.SendAttendanceEventAsync(pendingRequests, ct);
                        await _sessionService.UpdateLastHeartbeatAsync(ct);
                        foreach (var req in pendingRequests)
                        {
                            await _eventService.UpdateAsync(req.EventId, ct);
                        }
                    }
                }
                catch (SessionExpiredException ex)
                {
                    alreadyHandledForceLogin = true;
                    _logger.LogWarning(ex, "Auto Checkout failed because Session already expired");
                    await ForceReloginInternalAsync("Session expired during auto-checkout. Please login again.", ct);
                    return;
                }
                catch (ApiRequestException ex) when (ex.IsBusinessError)
                {
                    alreadyHandledForceLogin = true;
                    _logger.LogWarning(ex, "AUTO_CHECKOUT rejected by backend. Status={Status}, Body={Body}", (int)ex.StatusCode, ex.ResponseBody);
                    await ForceReloginInternalAsync("Auto-checkout rejected by server. Please login again.", ct);
                    return;
                }
                catch (Exception ex) when (IsNetworkLikeFailure(ex))
                {
                    _logger.LogWarning("Could not reach backend. Error={error}, Stored offline.", ex.Message);
                }
                finally
                {
                    // Fixed HIGH-4: Only execute generic force relogin if a specific business exception was not already handled
                    if (!alreadyHandledForceLogin)
                    {
                        await _sessionService.MarkCheckedoutAsync(currentSession.Id, checkoutTime, ct);
                        await ForceReloginInternalAsync("You were auto checked-out due to inactivity. Please login again.", ct);
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task OnIdleThresholdReachedAsync(DateTimeOffset idleStart, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                if (CurrentState == AppState.OnBreak)
                {
                    _logger.LogDebug("Idle threshold ignored because current state is OnBreak.");
                    return;
                }

                if (CurrentState != AppState.Working)
                {
                    _logger.LogDebug("Idle threshold ignored. CurrentState={CurrentState}", CurrentState);
                    return;
                }

                var session = _sessionService.CurrentSession;
                if (session is null)
                {
                    _logger.LogWarning("Idle threshold ignored because there is no active session.");
                    return;
                }

                session.CurrentIdleStartUtc = idleStart;
                await _repository.UpdateSessionIdleStartAsync(session.Id, idleStart, ct);
                await _eventService.StartIdleAsync(idleStart, ct);

                TransitionTo(AppState.IdleLocked);

                _logger.LogInformation("Idle threshold reached. IdleStart={IdleStart}", idleStart.ToUniversalTime().ToString("O"));

                LockRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task OnIdleEndRequestedAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                if (CurrentState != AppState.IdleLocked)
                {
                    return;
                }

                var session = _sessionService.CurrentSession;
                if (session is null)
                {
                    _logger.LogWarning("Idle end ignored because there is no active session.");
                    return;
                }

                session.CurrentIdleStartUtc = null;
                await _repository.UpdateSessionIdleStartAsync(session.Id, null, ct);
                await _eventService.EndIdleAsync(ct);

                TransitionTo(AppState.Working);

                _logger.LogInformation("Idle ended. Returning to Working state.");

                UnlockRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task OnBreakStartRequestedAsync(string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Break reason is required.", nameof(reason));
            }

            await _gate.WaitAsync(ct);

            try
            {
                if (CurrentState != AppState.Working)
                {
                    _logger.LogWarning("Break start ignored because current state is {State}.", CurrentState);
                    return;
                }

                var session = _sessionService.CurrentSession;
                if (session is null)
                {
                    _logger.LogWarning("Break start ignored because no active session exists.");
                    return;
                }

                await _eventService.StartBreakAsync(reason, ct);

                await _sessionService.UpdateStatusAsync(session.Id, SessionStatusTypes.ON_BREAK, ct);

                TransitionTo(AppState.OnBreak);

                _logger.LogInformation("Break started. SessionId={SessionId}, Reason={Reason}", session.Id, reason);

                BreakStartRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task OnBreakEndRequestedAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                if (CurrentState != AppState.OnBreak)
                {
                    _logger.LogWarning("Break end ignored because current state is {State}.", CurrentState);
                    return;
                }

                var session = _sessionService.CurrentSession;
                if (session is null)
                {
                    _logger.LogWarning("Break end ignored because no active session exists.");
                    return;
                }

                await _eventService.EndBreakAsync(ct);

                await _sessionService.UpdateStatusAsync(session.Id, SessionStatusTypes.WORKING, ct);

                TransitionTo(AppState.Working);

                _logger.LogInformation("Break ended. SessionId={SessionId}", session.Id);

                BreakEndRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task OnAutoCheckoutWarningAsync(TimeSpan remainingTime, CancellationToken ct = default)
        {
            if (CurrentState != AppState.IdleLocked)
            {
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "Auto-checkout warning requested. RemainingMinutes={RemainingMinutes}, RemainingSeconds={RemainingSeconds}",
                Math.Max(0, (int)Math.Ceiling(remainingTime.TotalMinutes)),
                Math.Max(0, (int)Math.Ceiling(remainingTime.TotalSeconds)));

            AutoCheckoutWarningRequested?.Invoke(this, new AutoCheckoutWarningEventArgs(remainingTime));

            return Task.CompletedTask;
        }

        public void TransitionTo(AppState state)
        {
            if (CurrentState == state)
            {
                return;
            }

            var oldState = CurrentState;
            CurrentState = state;

            _logger.LogInformation("State Changed: {old} => {new}", oldState, state);

            StateChanged?.Invoke(this, new AppStateChangedEventArgs(oldState, state));
        }

        public async Task ForceReloginAsync(string reason, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                await ForceReloginInternalAsync(reason, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        

        public async Task OnAutoCheckoutWarningAcknowledgedAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                if (CurrentState != AppState.IdleLocked)
                {
                    _logger.LogWarning("Auto-checkout warning acknowledgement ignored because current state is {State}.", CurrentState);
                    return;
                }

                // Acknowledged: keep locked until pin unlock, but reset idle timestamp
                var session = _sessionService.CurrentSession;
                if (session != null)
                {
                    session.CurrentIdleStartUtc = _clockService.UtcNow;
                    await _repository.UpdateSessionIdleStartAsync(session.Id, session.CurrentIdleStartUtc, ct);
                }

                _logger.LogInformation("Auto-checkout warning acknowledged. Idle baseline reset.");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task OnShutdownCheckoutAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);

            try
            {
                // No active attendance session = nothing to checkout.
                if (CurrentState == AppState.LoggedOut ||
                    CurrentState == AppState.CheckedOut)
                {
                    _logger.LogInformation(
                        "Shutdown checkout skipped. CurrentState={State}",
                        CurrentState);

                    return;
                }

                var currentSession = _sessionService.CurrentSession;

                if (currentSession is null)
                {
                    _logger.LogWarning(
                        "Shutdown checkout skipped. No active session found.");

                    return;
                }

                var checkoutTime = _clockService.UtcNow;

                var employee =await _employeeService.GetEmployeeBySessionIdAsync(
                        currentSession.Id,ct);

                var email =employee?.Email ??
                    _employeeService.Employee?.Email ??
                    "system";

                _logger.LogInformation(
                    "Windows shutdown detected. Creating shutdown checkout. SessionId={SessionId}, CheckoutTime={CheckoutTime}",
                    currentSession.Id,
                    checkoutTime);

                var checkoutEvent = new EventsEntity
                {
                    Id = Guid.NewGuid(),
                    LocalSessionId = currentSession.Id,
                    EventTime = _clockService.ToUtcIso(checkoutTime),
                    EventType = EventTypes.CHECK_OUT,
                    IsSynced = false,
                    Version = 0,
                    CreatedAt = checkoutTime,
                    UpdatedAt = checkoutTime,
                    CreatedBy = email,
                    UpdatedBy = email,
                    MetaData = "Shutdown checkout."
                };

                // 1. Always save locally first.
                await _repository.SaveEventAsync(checkoutEvent, ct);

                try
                {
                    // 2. Send pending events, including shutdown checkout.
                    var pendingRequests =
                        await _eventService.GetAllPendingEventsAsync(ct);

                    if (pendingRequests is not null &&
                        pendingRequests.Count > 0)
                    {
                        if (!pendingRequests.Any(
                                x => x.EventId == checkoutEvent.Id))
                        {
                            pendingRequests.Add(
                                new AttendanceEventRequest
                                {
                                    SessionId = currentSession.Id,
                                    EventId = checkoutEvent.Id,
                                    EmployeeId = currentSession.EmployeeId,
                                    EventTime = checkoutEvent.EventTime,
                                    EventType = checkoutEvent.EventType,
                                    MetaData = checkoutEvent.MetaData,
                                    IsOffline = checkoutEvent.Version == 0
                                });
                        }

                        await _apiClient.SendAttendanceEventAsync(
                            pendingRequests,
                            ct);

                        // 3. Mark successfully sent events as synced.
                        foreach (var request in pendingRequests)
                        {
                            await _eventService.UpdateAsync(
                                request.EventId,ct);
                        }

                        _logger.LogInformation(
                            "Shutdown checkout successfully sent to server. SessionId={SessionId}",
                            currentSession.Id);
                    }
                }
                catch (Exception ex) when (IsNetworkLikeFailure(ex))
                {
                    // Event is safely stored locally.
                    // It can be synced later when the application runs again.
                    _logger.LogWarning(
                        ex,
                        "Shutdown checkout could not reach backend. Checkout event remains queued locally.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected error while sending shutdown checkout.");
                }

                // 4. Close the local attendance session.
                await _sessionService.MarkCheckedoutAsync(currentSession.Id,checkoutTime,ct);

                TransitionTo(AppState.CheckedOut);

                _logger.LogInformation(
                    "Local attendance session closed because of Windows shutdown. SessionId={SessionId}",
                    currentSession.Id);
            }
            finally
            {
                _gate.Release();
            }
        }

        private static bool IsNetworkLikeFailure(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex is TimeoutException ||
                   ex.InnerException is HttpRequestException ||
                   ex.InnerException is TaskCanceledException ||
                   ex.InnerException is TimeoutException;
        }
    }
}