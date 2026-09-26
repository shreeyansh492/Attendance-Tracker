using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Tracker.Configuration;

namespace Tracker.Services
{
    public sealed class ClockService : IClockService
    {
        private readonly TimeZoneInfo _companyTimeZone;
        private readonly ILogger<ClockService> _logger;

        public ClockService(AppOptions appOptions, ILogger<ClockService> logger)
        {
            _logger = logger;
            _companyTimeZone = ResolveTimeZone(appOptions.CompanyTimeZone);

            _logger.LogInformation("Clock service intialized. CompanyTimeZone={TimeZoneId}", _companyTimeZone.Id);
        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public DateTimeOffset ToCompanyTime(DateTimeOffset time)
        {
            var normalizedUtc = time.ToUniversalTime();

            var companyDateTime = TimeZoneInfo.ConvertTime(normalizedUtc, _companyTimeZone);
            return companyDateTime;
        }

        public string FormatCompanyDateTime(DateTimeOffset time)
        {
            var companyTime = ToCompanyTime(time);
            return companyTime.ToString("dd-MMM-yyyy hh:mm:ss tt");
        }

        public string FormatCompanyTime(DateTimeOffset time)
        {
            var companyTime = ToCompanyTime(time);
            return companyTime.ToString("hh:mm:ss tt");
        }

        public string ToUtcIso(DateTimeOffset time)
        {
            return time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
                return TimeZoneInfo.Utc;

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                var windowsId = TryMapIanaToWindows(timeZoneId);

                if (!string.IsNullOrWhiteSpace(windowsId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }

                throw new InvalidOperationException(
                    $"Timezone '{timeZoneId}' was not found on this Windows system.");
            }
            catch (InvalidTimeZoneException ex)
            {
                throw new InvalidOperationException(
                    $"Timezone '{timeZoneId}' is invalid on this Windows system.",
                    ex);
            }
        }

        private static string? TryMapIanaToWindows(string timeZoneId)
        {
            if (string.Equals(
                    timeZoneId,
                    "Asia/Kolkata",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "India Standard Time";
            }

            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(
                    timeZoneId,
                    out var windowsId))
            {
                return windowsId;
            }

            return null;
        }
    }
}
