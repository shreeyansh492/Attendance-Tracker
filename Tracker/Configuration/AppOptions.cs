using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Configuration
{
    public sealed class AppOptions
    {
        public const string SectionName = "App";

        public string Name { get; set; } = "Tracker";

        public string CompanyTimeZone { get; set; } = "Asia/Kolkata";

        public string Environment { get; set; } = "Development";

        public bool IsDevelopment => string.Equals(Environment, "Development", StringComparison.OrdinalIgnoreCase);

        public bool IsProduction => string.Equals(Environment, "Production", StringComparison.OrdinalIgnoreCase);

    }
}
