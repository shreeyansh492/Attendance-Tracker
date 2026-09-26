using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Services
{
    public interface IClockService
    {
        DateTimeOffset UtcNow { get; }

        DateTimeOffset ToCompanyTime(DateTimeOffset utcTime);

        string FormatCompanyDateTime(DateTimeOffset utcTime);

        string FormatCompanyTime(DateTimeOffset utcTime);

        //Converts any DateTimeOffset to normalized UTC ISO-8601 string.
        string ToUtcIso(DateTimeOffset utcTime);
    }
}
