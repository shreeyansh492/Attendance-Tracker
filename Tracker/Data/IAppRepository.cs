using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Data
{
    public interface IAppRepository
    {
        Task EnsureDatabaseAsync(CancellationToken ct = default);

        Task AddSessionAsync(LocalSessionEntity session, CancellationToken ct = default);
        Task<LocalSessionEntity?> GetSessionByEmployeeId(Guid employeeId, CancellationToken ct = default);
        Task UpdateSessionStatusAsync(Guid sessionId, SessionStatusTypes sessionStatus, CancellationToken ct = default);
        Task MarkCheckedoutAsync(Guid sessionId, DateTimeOffset checkoutTime, CancellationToken ct = default);
        Task UpdateSessionIdleStartAsync(Guid sessionId, DateTimeOffset? idleStartUtc, CancellationToken ct = default);
        Task UpdateLastHeartbeatAsync(Guid sessionId, DateTimeOffset heartbeatTime, CancellationToken ct = default);
        Task<List<AttendanceEventRequest>?> GetAllPendingEventsAsync(CancellationToken ct = default);

        Task<EmployeeEntity?> GetEmployeeByEmailAsync(string email, CancellationToken ct = default);
        Task SaveEmployeeAsync(EmployeeEntity employee, CancellationToken ct = default);
        Task DeleteEmployeeRecordAsync(string email, CancellationToken ct = default);
        Task<EmployeeEntity?> GetEmployeeBySessionIdAsync(Guid sessionId, CancellationToken ct = default);

        Task SaveEventAsync(EventsEntity events, CancellationToken ct = default);
        Task UpdateEventAsync(Guid eventId, CancellationToken ct = default);

        Task DeleteOldSyncedEventsAsync(DateTimeOffset cutoff, CancellationToken ct = default);
        Task DeleteOldEmployeesAsync(DateTimeOffset cutoff, CancellationToken ct = default);
        Task DeleteOldClosedSessionsAsync(DateTimeOffset cutoff, CancellationToken ct = default);

        Task<LocalSessionEntity?> GetSessionByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default);
    }
}