using System;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Services
{
    public interface ISessionService
    {
        LocalSessionEntity? CurrentSession { get; }

        bool HasActiveSession { get; }

        Task<LocalSessionEntity> LoadOrCreateSessionAfterLoginAsync(
            PinLoginResponse loginResponse, CancellationToken ct = default);

        Task MarkCheckedoutAsync(Guid localSessionId, DateTimeOffset checkoutTime, CancellationToken ct = default);

        Task<LocalSessionEntity?> LoadSessionByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);

        Task UpdateStatusAsync(Guid sessionId, SessionStatusTypes status, CancellationToken ct = default);

        Task UpdateLastHeartbeatAsync(CancellationToken ct = default);

        // Fixed HIGH-2: Changed signature to return an awaitable Task
        Task ClearCurrentSessionAsync();
    }
}