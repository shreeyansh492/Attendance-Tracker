using System;

namespace Tracker.Configuration
{
    public sealed class DiagnosticsOptions
    {
        public const string SectionName = "Diagnostics";

        // Fixed MED-2: Spelled correctly so appsettings.json can map to it
        public bool EnableDiagnosticsWindow { get; set; } = true;

        public int InMemoryLogLineLimit { get; set; } = 500;

        public int LogRetentionDays { get; set; } = 14;

        public void Validate()
        {
            if (InMemoryLogLineLimit <= 100)
            {
                throw new InvalidOperationException("Diagnostics: InMemoryLogLineLimit must be at least 100.");
            }
            if (LogRetentionDays <= 0)
            {
                throw new InvalidOperationException("Diagnostics: LogRetentionDays must be greater than zero.");
            }
        }
    }
}