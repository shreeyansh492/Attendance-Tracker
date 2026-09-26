using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Data.Entity
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }

        public int Version { get; set; } = 0;

        public string UpdatedBy { get; set; } = string.Empty;
        
        public DateTimeOffset UpdatedAt { get; set; }

        public string CreatedBy {  get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }
}
