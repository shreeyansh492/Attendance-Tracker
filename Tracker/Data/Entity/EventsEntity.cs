using System;

namespace Tracker.Data.Entity
{
    public sealed class EventsEntity : BaseEntity
    {
        public Guid LocalSessionId { get; set; }

        public EventTypes EventType { get; set; }

        public string EventTime { get; set; } = string.Empty;

        public string? MetaData { get; set; }

        public bool IsSynced { get; set; }
    }
}