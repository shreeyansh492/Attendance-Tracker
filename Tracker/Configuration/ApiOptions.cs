using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Configuration
{
    public sealed class ApiOptions
    {
        public const string SectionName = "Api";

        public string BaseUrl { get; set; } = string.Empty;

        public string WindowsAppKey { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 15;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                throw new InvalidOperationException("Api:BaseUrl is missing in appsettings.json.");
            }
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"BaseUrl = {BaseUrl} is not valid.");
            }
            if (string.IsNullOrWhiteSpace(WindowsAppKey))
            {
                throw new InvalidOperationException("Api: WindowsAppKey is missing in appsettings.json");
            }
            if(TimeoutSeconds <= 0)
            {
                throw new InvalidOperationException("Api: TimeoutSeconds must be greater than zero.");
            }
        }
    }
}
