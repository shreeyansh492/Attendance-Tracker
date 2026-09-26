using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Data.Entity;
using Tracker.Dto;
using Tracker.Services;

namespace Tracker.Data
{
    public sealed class AppRepository : IAppRepository
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly ILogger<AppRepository> _logger;
        private readonly IClockService _clock;

        public AppRepository(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<AppRepository> logger, IClockService clock)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _clock = clock;
        }

        public async Task EnsureDatabaseAsync(CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            await db.Database.EnsureCreatedAsync(ct);
        }

        public async Task AddSessionAsync(LocalSessionEntity session, CancellationToken ct = default)
        {
            if (session.Id == Guid.Empty)
            {
                session.Id = Guid.NewGuid();
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            db.LocalSessions.Add(session);
            await db.SaveChangesAsync(ct);
        }

        public async Task<LocalSessionEntity?> GetSessionByEmployeeId(Guid employeeId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // Restore the 12-hour window for overnight shifts
            var cutoff = DateTimeOffset.UtcNow.AddHours(-12);

            var sessions = await db.LocalSessions
                .AsNoTracking()
                .Where(x => x.EmployeeId == employeeId && x.Status != SessionStatusTypes.CLOSED)
                .ToListAsync(ct);

            // Filter by the cutoff in memory and grab the most recent valid shift
            return sessions
                .Where(x => x.CheckInTime >= cutoff)
                .OrderByDescending(x => x.CheckInTime)
                .FirstOrDefault();
        }

        public async Task UpdateSessionStatusAsync(Guid sessionId, SessionStatusTypes status, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            await db.LocalSessions.Where(x => x.Id == sessionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, status)
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.Now), ct);
        }

        public async Task MarkCheckedoutAsync(Guid sessionId, DateTimeOffset checkoutTime, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            await db.LocalSessions.Where(x => x.Id == sessionId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, SessionStatusTypes.CLOSED)
            .SetProperty(x => x.CheckOutTime, checkoutTime).SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.UpdatedAt, DateTimeOffset.Now), ct);
        }

        public async Task UpdateLastHeartbeatAsync(Guid sessionId, DateTimeOffset time, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            await db.LocalSessions.Where(x => x.Id == sessionId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastHeartbeatTime, time)
            .SetProperty(x => x.Version, x => x.Version + 1), ct);
        }

        public async Task<EmployeeEntity?> GetEmployeeByEmailAsync(string email, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            return await db.EmployeeRecord.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);
        }

        public async Task SaveEmployeeAsync(EmployeeEntity employee, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            bool exists = await db.EmployeeRecord.AnyAsync(e => e.Id == employee.Id, ct);

            if (exists)
            {
                db.EmployeeRecord.Update(employee);
            }
            else
            {
                await db.EmployeeRecord.AddAsync(employee, ct);
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict detected while saving employee {Email}.", employee.Email);
                throw;
            }
        }

        public async Task DeleteEmployeeRecordAsync(string email, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            int rowsAffected = await db.EmployeeRecord.Where(e => e.Email == email).ExecuteDeleteAsync(ct);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("No employee record found to delete for email: {Email}", email);
            }
            else
            {
                _logger.LogInformation("Successfully deleted employee record for email: {Email}", email);
            }
        }

        public async Task<EmployeeEntity?> GetEmployeeBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var session = await db.LocalSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

            if (session == null)
            {
                return null;
            }

            return await db.EmployeeRecord
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeId == session.EmployeeId, ct);
        }

        public async Task<List<AttendanceEventRequest>?> GetAllPendingEventsAsync(CancellationToken ct = default)
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
                var pendingEvents = await db.Events.Where(e => e.IsSynced == false).Join(
                    db.LocalSessions, evt => evt.LocalSessionId, session => session.Id, (evt, session) => new AttendanceEventRequest
                    {
                        SessionId = session.Id,
                        EventId = evt.Id,
                        EmployeeId = session.EmployeeId,
                        EventTime = evt.EventTime,
                        EventType = evt.EventType,
                        MetaData = evt.MetaData,
                        IsOffline = evt.Version == 0,
                    }
                    ).ToListAsync(ct);

                return pendingEvents;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch pending events");
                return new List<AttendanceEventRequest>();
            }
        }

        public async Task UpdateSessionIdleStartAsync(Guid sessionId, DateTimeOffset? idleStartUtc, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            await db.LocalSessions.Where(x => x.Id == sessionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CurrentIdleStartUtc, idleStartUtc)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.Now), ct);
        }

        public async Task SaveEventAsync(EventsEntity events, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            bool exists = await db.Events.AnyAsync(e => e.Id == events.Id, ct);

            if (exists)
            {
                db.Events.Update(events);
            }
            else
            {
                await db.Events.AddAsync(events, ct);
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict detected while saving event {EventType}.", events.EventType);
                throw;
            }
        }

        public async Task UpdateEventAsync(Guid eventId, CancellationToken ct = default)
        {
            var now = _clock.UtcNow;
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            await db.Events
                .Where(x => x.Id == eventId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsSynced, true)
                    .SetProperty(x => x.UpdatedAt, now)
                    .SetProperty(x => x.Version, x => x.Version + 1),
                    ct);
        }

        public async Task DeleteOldSyncedEventsAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var eventsToDelete = await db.Events
                .AsNoTracking()
                .Where(x => x.IsSynced)
                .ToListAsync(ct);

            var filteredEvents = eventsToDelete
                .Where(x => x.UpdatedAt < cutoff)
                .ToList();

            if (filteredEvents.Count > 0)
            {
                db.Events.RemoveRange(filteredEvents);
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task DeleteOldEmployeesAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var activeEmployeeIds = await db.LocalSessions
                .AsNoTracking()
                .Where(x =>
                    x.Status == SessionStatusTypes.WORKING ||
                    x.Status == SessionStatusTypes.IDLE ||
                    x.Status == SessionStatusTypes.ON_BREAK)
                .Select(x => x.EmployeeId)
                .Distinct()
                .ToListAsync(ct);

            var candidates = await db.EmployeeRecord
                .Where(x => !activeEmployeeIds.Contains(x.EmployeeId))
                .ToListAsync(ct);

            var employeesToDelete = candidates
                .Where(x => x.UpdatedAt < cutoff)
                .ToList();

            if (employeesToDelete.Count > 0)
            {
                db.EmployeeRecord.RemoveRange(employeesToDelete);
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task DeleteOldClosedSessionsAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var closedSessions = await db.LocalSessions
                .Where(x => x.Status == SessionStatusTypes.CLOSED)
                .ToListAsync(ct);

            var sessionsToDelete = closedSessions
                .Where(x => x.CheckInTime < cutoff)
                .ToList();

            if (sessionsToDelete.Count > 0)
            {
                db.LocalSessions.RemoveRange(sessionsToDelete);
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task<LocalSessionEntity?> GetSessionByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // Fetch matching sessions for the date without ordering in SQL
            var sessions = await db.LocalSessions
                .AsNoTracking()
                .Where(x => x.EmployeeId == employeeId && x.Date == date)
                .ToListAsync(ct);

            // Order on the client side using LINQ to Objects to avoid SQLite DateTimeOffset limitations
            return sessions
                .OrderByDescending(x => x.CheckInTime)
                .FirstOrDefault();
        }
    }
}