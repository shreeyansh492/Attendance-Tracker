using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Api;
using Tracker.Core;
using Tracker.Data;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Services
{
    public sealed class EventService : IEventService
    {
        private readonly IAppRepository _repository;
        private readonly ILogger<EventService> _logger;
        private readonly IClockService _clock;
        private readonly ISessionService _sessionService;
        private readonly IApiClient _apiClient;
        private readonly IEmployeeService _employeeService;

        public EventService(
            IAppRepository repository,
            ILogger<EventService> logger,
            IClockService clock,
            ISessionService sessionService,
            IApiClient apiClient,
            IEmployeeService employeeService)
        {
            _repository = repository;
            _logger = logger;
            _clock = clock;
            _sessionService = sessionService;
            _apiClient = apiClient;
            _employeeService = employeeService;
        }

        public async Task<List<AttendanceEventRequest>?> GetAllPendingEventsAsync(CancellationToken ct = default)
        {
            try
            {
                var pendingEvents = await _repository.GetAllPendingEventsAsync(ct);
                return pendingEvents ?? new List<AttendanceEventRequest>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        public async Task AutoCheckinAsync(Guid sessionId, string email, CancellationToken ct = default)
        {
            var now = _clock.UtcNow;

            var checkinEvent = new EventsEntity
            {
                Id = Guid.NewGuid(),
                LocalSessionId = sessionId,
                EventType = EventTypes.CHECK_IN,
                EventTime = _clock.ToUtcIso(now),
                IsSynced = false,
                UpdatedAt = now,
                UpdatedBy = email,
                CreatedAt = now,
                CreatedBy = email,
                MetaData = "Auto Checkin Event"
            };

            try
            {
                await _repository.SaveEventAsync(checkinEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving checkin event {EventId} to database.", checkinEvent.Id);
                throw;
            }

            try
            {
                var pendingRequests = await GetAllPendingEventsAsync(ct);

                if (pendingRequests is not null && pendingRequests.Count > 0)
                {
                    await _apiClient.SendAttendanceEventAsync(pendingRequests, ct);

                    foreach (var request in pendingRequests)
                    {
                        await UpdateAsync(request.EventId, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to sync attendance events to API. Error = {Error}", ex.Message);
            }
        }

        public async Task UpdateAsync(Guid eventId, CancellationToken ct = default)
        {
            try
            {
                await _repository.UpdateEventAsync(eventId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Fatal Error occurred while updating the event. Id={eventId} Error={Error}", eventId, ex.Message);
            }
        }

        public async Task StartBreakAsync(string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Break reason is required.", nameof(reason));
            }

            var session = _sessionService.CurrentSession;
            if (session is null)
            {
                throw new InvalidOperationException("No active session found.");
            }

            if (session.Status == SessionStatusTypes.ON_BREAK)
            {
                throw new InvalidOperationException("Break is already active.");
            }

            var now = _clock.UtcNow;

            var breakStartEvent = new EventsEntity
            {
                Id = Guid.NewGuid(),
                LocalSessionId = session.Id,
                EventType = EventTypes.BREAK_START,
                EventTime = _clock.ToUtcIso(now),
                MetaData = reason,
                IsSynced = false,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = session.CreatedBy,
                UpdatedBy = session.UpdatedBy
            };

            await _repository.SaveEventAsync(breakStartEvent, ct);

            try
            {
                var pendingRequests = await GetAllPendingEventsAsync(ct) ?? new List<AttendanceEventRequest>();

                if (!pendingRequests.Any(x => x.EventId == breakStartEvent.Id))
                {
                    pendingRequests.Add(new AttendanceEventRequest
                    {
                        SessionId = session.Id,
                        EventId = breakStartEvent.Id,
                        EmployeeId = session.EmployeeId,
                        EventTime = breakStartEvent.EventTime,
                        EventType = breakStartEvent.EventType,
                        MetaData = breakStartEvent.MetaData,
                        IsOffline = breakStartEvent.Version == 0
                    });
                }

                if (pendingRequests.Count > 0)
                {
                    await _apiClient.SendAttendanceEventAsync(pendingRequests, ct);

                    foreach (var request in pendingRequests)
                    {
                        await UpdateAsync(request.EventId, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to sync break-start event to API. Error={Error}", ex.Message);
            }

            _logger.LogInformation("Break started. SessionId={SessionId}, Reason={Reason}", session.Id, reason);
        }

        public async Task EndBreakAsync(CancellationToken ct = default)
        {
            var session = _sessionService.CurrentSession;
            if (session is null)
            {
                throw new InvalidOperationException("No active session found.");
            }

            if (session.Status != SessionStatusTypes.ON_BREAK)
            {
                throw new InvalidOperationException("No active break to end.");
            }

            var now = _clock.UtcNow;

            var breakEndEvent = new EventsEntity
            {
                Id = Guid.NewGuid(),
                LocalSessionId = session.Id,
                EventType = EventTypes.BREAK_END,
                EventTime = _clock.ToUtcIso(now),
                MetaData = "Break_end",
                IsSynced = false,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = session.CreatedBy,
                UpdatedBy = session.UpdatedBy
            };

            await _repository.SaveEventAsync(breakEndEvent, ct);

            try
            {
                var breakEndRequest = new AttendanceEventRequest
                {
                    SessionId = session.Id,
                    EventId = breakEndEvent.Id,
                    EmployeeId = session.EmployeeId,
                    EventTime = breakEndEvent.EventTime,
                    EventType = breakEndEvent.EventType,
                    MetaData = breakEndEvent.MetaData,
                    IsOffline = breakEndEvent.Version == 0
                };

                await _apiClient.SendAttendanceEventAsync(new List<AttendanceEventRequest> { breakEndRequest }, ct);
                await UpdateAsync(breakEndEvent.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to sync break-end event to API. Error={Error}", ex.Message);
            }

            _logger.LogInformation("Break ended. SessionId={SessionId}", session.Id);
        }

        public async Task StartIdleAsync(DateTimeOffset idleStart, CancellationToken ct = default)
        {
            var session = _sessionService.CurrentSession;
            if (session is null)
            {
                throw new InvalidOperationException("No active session found.");
            }

            if (session.Status == SessionStatusTypes.ON_BREAK)
            {
                _logger.LogDebug("IDLE_START ignored because the session is on break.");
                return;
            }

            if (idleStart > _clock.UtcNow)
            {
                throw new ArgumentException("Idle start time cannot be in the future.", nameof(idleStart));
            }

            var idleStartEvent = new EventsEntity
            {
                Id = Guid.NewGuid(),
                LocalSessionId = session.Id,
                EventType = EventTypes.IDLE_START,
                EventTime = _clock.ToUtcIso(idleStart),
                MetaData = "Idle_Start",
                IsSynced = false,
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                CreatedBy = session.CreatedBy,
                UpdatedBy = session.UpdatedBy
            };

            await _repository.SaveEventAsync(idleStartEvent, ct);

            _logger.LogInformation("Idle started. SessionId={SessionId}, IdleStart={IdleStart}", session.Id, idleStart.ToUniversalTime().ToString("O"));
        }

        public async Task EndIdleAsync(CancellationToken ct = default)
        {
            var session = _sessionService.CurrentSession;
            if (session is null)
            {
                throw new InvalidOperationException("No active session found.");
            }

            var idleEndEvent = new EventsEntity
            {
                Id = Guid.NewGuid(),
                LocalSessionId = session.Id,
                EventType = EventTypes.IDLE_END,
                EventTime = _clock.ToUtcIso(_clock.UtcNow),
                MetaData = "Idle_End",
                IsSynced = false,
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                CreatedBy = session.CreatedBy,
                UpdatedBy = session.UpdatedBy
            };

            await _repository.SaveEventAsync(idleEndEvent, ct);

            _logger.LogInformation("Idle ended. SessionId={SessionId}", session.Id);
        }

        public async Task SyncPendingEventsAsync(CancellationToken ct = default)
        {
            try
            {
                var pendingRequests = await GetAllPendingEventsAsync(ct);

                if (pendingRequests == null || pendingRequests.Count == 0)
                {
                    return; // Nothing to sync
                }

                _logger.LogInformation("Attempting to sync {Count} offline events to backend...", pendingRequests.Count);

                // Attempt to send the batch to the Spring Boot backend
                await _apiClient.SendAttendanceEventAsync(pendingRequests, ct);

                // If successful (no exceptions thrown), mark them all as synced in SQLite
                foreach (var request in pendingRequests)
                {
                    await UpdateAsync(request.EventId, ct);
                }

                _logger.LogInformation("Successfully synced {Count} offline events.", pendingRequests.Count);
            }
            catch (SessionExpiredException ex)
            {
                _logger.LogWarning(ex, "Background sync failed. Token is expired or missing. Will retry on next login.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background sync failed due to network/server error. Will retry later.");
            }
        }
    }
}