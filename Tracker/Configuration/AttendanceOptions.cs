using System;

namespace Tracker.Configuration
{
    public sealed class AttendanceOptions
    {
        public const string SectionName = "Attendance";

        public int IdleThresholdSeconds { get; set; } = 180;

        public int AutoCheckoutMinutes { get; set; } = 30;

        public int WarningBeforeAutoCheckoutMinutes { get; set; } = 5;

        public int HeartbeatIntervalSeconds { get; set; } = 60;

        public int DeleteRecordAfterDays { get; set; } = 7;

        // Fixed MED-3: Added the missing configuration properties defined in appsettings.json
        public int SyncIntervalSeconds { get; set; } = 300;

        public int OfflineMaxAttempts { get; set; } = 5;

        public void Validate()
        {
            if (IdleThresholdSeconds <= 0)
            {
                throw new InvalidOperationException("Attendance: IdleThresholdSeconds must be greater than zero.");
            }
            if (AutoCheckoutMinutes <= 0)
            {
                throw new InvalidOperationException("Attendance: AutoCheckoutMinutes must be greater than zero.");
            }
            if (WarningBeforeAutoCheckoutMinutes <= 0)
            {
                throw new InvalidOperationException("Attendance: WarningBeforeAutoCheckoutMinutes must be greater than zero.");
            }
            if (HeartbeatIntervalSeconds <= 0)
            {
                throw new InvalidOperationException("Attendance: HeartbeatIntervalSeconds must be greater than zero.");
            }
            if (WarningBeforeAutoCheckoutMinutes >= AutoCheckoutMinutes)
            {
                throw new InvalidOperationException("Attendance: AutoCheckoutMinutes must be greater than WarningBeforeAutoCheckoutMinutes.");
            }
            if (DeleteRecordAfterDays <= 0)
            {
                throw new InvalidOperationException("Attendance: DeleteRecordAfterDays must be greater than zero.");
            }
            if (SyncIntervalSeconds <= 0)
            {
                throw new InvalidOperationException("Attendance: SyncIntervalSeconds must be greater than zero.");
            }
            if (OfflineMaxAttempts <= 0)
            {
                throw new InvalidOperationException("Attendance: OfflineMaxAttempts must be greater than zero.");
            }
        }
    }
}