using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.Configuration;
using Tracker.Data.Entity;
using Tracker.Machine;
using Tracker.Services;

namespace Tracker.Workers;

public sealed class IdleMonitorWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(5);

    private readonly IIdleDetectionService _idleDetectionService;
    private readonly IAppStateService _appStateService;
    private readonly IClockService _clockService;
    private readonly AttendanceOptions _attendanceOptions;
    private readonly ILogger<IdleMonitorWorker> _logger;
    private readonly ISessionService _sessionService;

    private bool _idleTriggered;
    private bool _autoCheckoutWarningTriggered;
    private bool _autoCheckoutTriggered;
    private DateTimeOffset? _breakEndedAt;
    private bool _wasOnBreak;
    public IdleMonitorWorker(
        IIdleDetectionService idleDetectionService,
        IAppStateService appStateService,
        IClockService clockService,
        AttendanceOptions attendanceOptions,
        ISessionService sessionService,
        ILogger<IdleMonitorWorker> logger)
    {
        _idleDetectionService = idleDetectionService;
        _appStateService = appStateService;
        _clockService = clockService;
        _attendanceOptions = attendanceOptions;
        _logger = logger;
        _sessionService = sessionService;
        
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Idle monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckIdleAsync(stoppingToken);

                await Task.Delay(
                    PollInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in idle monitor.");
            }
        }

        _logger.LogInformation("Idle monitor stopped.");
    }

    private async Task CheckIdleAsync(
        CancellationToken ct)
    {
        var isOnBreak = _appStateService.CurrentState == AppState.OnBreak;
        // Never calculate idle during break.
        if (isOnBreak)
        {
            _wasOnBreak = true;
            _idleTriggered = false;
            _autoCheckoutWarningTriggered = false;
            _autoCheckoutTriggered = false;
            _breakEndedAt = null;
            return;
        }

        if (_appStateService.CurrentState == AppState.IdleLocked)
        {
            // Check whether the user has become active again.
            var currentIdleTime = _idleDetectionService.GetIdleTime();

            if (currentIdleTime < TimeSpan.FromSeconds(
                    _attendanceOptions.IdleThresholdSeconds))
            {
                _logger.LogInformation(
                    "User activity detected. Ending idle state.");

                _idleTriggered = false;
                _autoCheckoutWarningTriggered = false;
                _autoCheckoutTriggered = false;

                await _appStateService.OnIdleEndRequestedAsync(ct);

                return;
            }

            var session = _sessionService.CurrentSession;

            if (session is null ||
                session.CurrentIdleStartUtc is null)
            {
                _logger.LogWarning(
                    "App is IdleLocked but idle start time is missing.");

                return;
            }

            var idleDuration =
                _clockService.UtcNow - session.CurrentIdleStartUtc.Value;

            var autoCheckoutAfter = TimeSpan.FromMinutes(
                _attendanceOptions.AutoCheckoutMinutes);

            var warningAt = autoCheckoutAfter - TimeSpan.FromMinutes(
                _attendanceOptions.WarningBeforeAutoCheckoutMinutes);

            if (idleDuration >= warningAt &&
                idleDuration < autoCheckoutAfter)
            {
                if (!_autoCheckoutWarningTriggered)
                {
                    _autoCheckoutWarningTriggered = true;

                    var remaining = autoCheckoutAfter - idleDuration;

                    await _appStateService.OnAutoCheckoutWarningAsync(
                        remaining,
                        ct);
                }
            }

            if (idleDuration >= autoCheckoutAfter)
            {
                if (_autoCheckoutTriggered)
                {
                    return;
                }

                _autoCheckoutTriggered = true;

                await _appStateService.OnAutoCheckoutAsync(ct);
            }

            return;
        }
       
        if (_wasOnBreak &&_appStateService.CurrentState == AppState.Working)
        {
            _wasOnBreak = false;
            _breakEndedAt = _clockService.UtcNow;
            _idleTriggered = false;
            _autoCheckoutWarningTriggered = false;
            _autoCheckoutTriggered = false;

            _logger.LogInformation(
                "Break ended. Idle detection baseline reset at {BreakEndedAt}.",
                _breakEndedAt.Value.ToUniversalTime().ToString("O"));

            return;
        }
        // Idle detection only when the app is in working state.
        if (_appStateService.CurrentState != AppState.Working)
        {
            _idleTriggered = false;
            _autoCheckoutWarningTriggered = false;
            _autoCheckoutTriggered = false;

            if (_appStateService.CurrentState == AppState.CheckedOut ||
                _appStateService.CurrentState == AppState.LoggedOut)
            {
                _breakEndedAt = null;
                _wasOnBreak = false;
            }

            return;
        }
        var threshold = TimeSpan.FromSeconds(
            _attendanceOptions.IdleThresholdSeconds);

        var idleTime = _idleDetectionService.GetIdleTime();
        if (_breakEndedAt is not null)
        {
            var timeSinceBreakEnd =
                _clockService.UtcNow - _breakEndedAt.Value;

            if (timeSinceBreakEnd < idleTime)
            {
                idleTime = timeSinceBreakEnd;
            }
        }

        if (idleTime < threshold)
        {
            _idleTriggered = false;
            _autoCheckoutWarningTriggered = false;
            _autoCheckoutTriggered = false;
            _breakEndedAt = null;
            return;
        }

        if (_idleTriggered)
        {
            return;
        }

        var idleStart = _clockService.UtcNow - idleTime;

        _logger.LogInformation(
            "Idle threshold reached. IdleSeconds={IdleSeconds}, IdleStart={IdleStart}",
            (int)idleTime.TotalSeconds,
            idleStart.ToUniversalTime().ToString("O"));

        await _appStateService.OnIdleThresholdReachedAsync(
            idleStart,
            ct);

        _idleTriggered = true;
    }
}