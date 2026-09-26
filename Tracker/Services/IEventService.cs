using System;
using System.Collections.Generic;
using System.Text;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Services
{
    public interface IEventService
    {

        Task<List<AttendanceEventRequest>?> GetAllPendingEventsAsync(CancellationToken ct = default);

        Task AutoCheckinAsync(Guid sessionId, string email, CancellationToken ct = default);

        Task UpdateAsync(Guid eventId, CancellationToken ct = default);
        

        // Creates and stores a BREAK_START event for the active session.
        Task StartBreakAsync( string reason,CancellationToken ct = default);

        // Creates and stores a BREAK_END event for the active session.
        Task EndBreakAsync(CancellationToken ct = default);

        // Creates and stores an IDLE_START event for the current session.
        Task StartIdleAsync(DateTimeOffset idleStart,CancellationToken ct = default);

        // Creates and stores an IDLE_END event for the current session.
        Task EndIdleAsync(CancellationToken ct = default);

        Task SyncPendingEventsAsync(CancellationToken ct = default);
    }
}
