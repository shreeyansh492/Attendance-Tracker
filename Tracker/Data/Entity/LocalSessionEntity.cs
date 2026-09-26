using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Data.Entity
{
    public sealed class LocalSessionEntity : BaseEntity
    {
        public Guid EmployeeId { get; set; }

        public SessionStatusTypes Status {  get; set; } = SessionStatusTypes.WORKING;

        public DateOnly Date {  get; set; }

        public DateTimeOffset CheckInTime { get; set; }

        public DateTimeOffset? CheckOutTime { get; set; }

        public DateTimeOffset? LastHeartbeatTime { get; set; }

        public DateTimeOffset? CurrentIdleStartUtc { get; set; }

        public bool IsOfflineSession { get; set; } = false;
    }
}
