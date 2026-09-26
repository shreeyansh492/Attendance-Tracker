using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Tracker.Dto
{
    public sealed class PinLoginResponse
    {
        [JsonPropertyName("employeeId")]
        public required Guid EmployeeId { get; set; }

        [JsonPropertyName("employeeCode")]
        public string? EmployeeCode { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string? EmployeeName { get; set; }

        [JsonPropertyName("department")]
        public string? Department { get; set; }

        [JsonPropertyName("designation")]
        public string? Designation { get; set; }

        [JsonPropertyName("sessionToken")]
        public required string SessionToken { get; set; }

        [JsonPropertyName("sessionExpiresInMs")]
        public long SessionExpiresInMs { get; set; }
    }
}
