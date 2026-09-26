using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Api;
using Tracker.Core;
using Tracker.Data;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Services
{
    public sealed class SessionService : ISessionService
    {
        private readonly IAppRepository _repository;
        private readonly IClockService _clockService;
        private readonly ILogger<SessionService> _logger;
        private readonly IApiClient _apiClient;

        public SessionService(
            IAppRepository repository,
            IClockService clockService,
            ILogger<SessionService> logger,
            IApiClient apiClient)
        {
            _repository = repository;
            _clockService = clockService;
            _logger = logger;
            _apiClient = apiClient;
        }

        public LocalSessionEntity? CurrentSession { get; private set; }

        public bool HasActiveSession => CurrentSession is not null &&
            (CurrentSession.Status == SessionStatusTypes.WORKING ||
             CurrentSession.Status == SessionStatusTypes.IDLE ||
             CurrentSession.Status == SessionStatusTypes.ON_BREAK);

        public async Task<LocalSessionEntity> LoadOrCreateSessionAfterLoginAsync(PinLoginResponse response, CancellationToken ct = default)
        {
            var isOnline = await _apiClient.IsApiOnlineAsync(ct);

            if (response.EmployeeId == Guid.Empty)
            {
                throw new InvalidOperationException("Login response employeeId is empty.");
            }
            if (string.IsNullOrWhiteSpace(response.Email))
            {
                throw new InvalidOperationException("Login response Email is missing");
            }

            var now = _clockService.UtcNow;
            var today = DateOnly.FromDateTime(now.LocalDateTime);

            // Look for an existing session for TODAY for this employee (even if it was closed)
            var session = await _repository.GetSessionByEmployeeAndDateAsync(response.EmployeeId, today, ct);

            if (session is null)
            {
                // Create a brand new session for today
                session = new LocalSessionEntity
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = response.EmployeeId,
                    Status = SessionStatusTypes.WORKING,
                    Date = today,
                    CheckInTime = now,
                    CheckOutTime = null,
                    LastHeartbeatTime = null,
                    IsOfflineSession = !isOnline,
                    CreatedAt = now,
                    CreatedBy = response.Email,
                    UpdatedBy = response.Email,
                    UpdatedAt = now,
                    Version = 0,
                };
                _logger.LogInformation("New Local session Created for today. Employee = {Email}, SessionId = {SessionId}", response.Email, session.Id);
                await _repository.AddSessionAsync(session, ct);
            }
            else
            {
                // Reopen/Resume today's existing session if it was closed or idle
                session.Status = SessionStatusTypes.WORKING;
                session.CheckOutTime = null; // Clear previous checkout time so they can continue working
                session.UpdatedAt = now;
                session.Version++;

                await _repository.UpdateSessionStatusAsync(session.Id, SessionStatusTypes.WORKING, ct);
                _logger.LogInformation("Resumed existing session for today. Employee = {Email}, SessionId = {SessionId}", response.Email, session.Id);
            }

            CurrentSession = session;
            return session;
        }

        public async Task UpdateStatusAsync(Guid sessionId, SessionStatusTypes status, CancellationToken ct = default)
        {
            await _repository.UpdateSessionStatusAsync(sessionId, status, ct);

            if (CurrentSession is not null && CurrentSession.Id == sessionId)
            {
                CurrentSession.Status = status;
            }
        }

        public async Task<LocalSessionEntity?> LoadSessionByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
        {
            return await _repository.GetSessionByEmployeeId(employeeId, ct);
        }

        public async Task MarkCheckedoutAsync(Guid sessionId, DateTimeOffset checkoutTime, CancellationToken ct = default)
        {
            try
            {
                await _repository.MarkCheckedoutAsync(sessionId, checkoutTime, ct);

                // Fixed HIGH-1: Synchronize the in-memory session object when a checkout occurs
                if (CurrentSession is not null && CurrentSession.Id == sessionId)
                {
                    CurrentSession.Status = SessionStatusTypes.CLOSED;
                    CurrentSession.CheckOutTime = checkoutTime;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Session update failed could not close session. sessionId={sessionId} Error={error}", sessionId, ex.Message);
                throw;
            }
        }

        public async Task UpdateLastHeartbeatAsync(CancellationToken ct = default)
        {
            if (CurrentSession is not null)
            {
                var time = _clockService.UtcNow;

                await _repository.UpdateLastHeartbeatAsync(
                    CurrentSession.Id,
                    time,
                    ct);

                CurrentSession.LastHeartbeatTime = time;
            }
        }

        // Fixed HIGH-2: Clear current session correctly without gating on a stale status field
        public async Task ClearCurrentSessionAsync()
        {
            if (CurrentSession is null)
            {
                return;
            }

            // Only perform a database checkout if the session isn't already closed.
            if (CurrentSession.Status != SessionStatusTypes.CLOSED)
            {
                await MarkCheckedoutAsync(CurrentSession.Id, _clockService.UtcNow);
            }

            CurrentSession = null;
        }
    }
}