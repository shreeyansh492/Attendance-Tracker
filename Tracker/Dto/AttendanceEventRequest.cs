using System;
using System.Text.Json.Serialization;
using Tracker.Data.Entity;

namespace Tracker.Dto
{
    public sealed class AttendanceEventRequest
    {
        [JsonPropertyName("clientShiftId")]
        public required Guid SessionId { get; set; }

        [JsonPropertyName("clientEventId")]
        public required Guid EventId { get; set; }

        [JsonPropertyName("employeeId")]
        public required Guid EmployeeId { get; set; }

        // Added JsonStringEnumConverter to align with Spring Boot backend requirements
        [JsonPropertyName("eventType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required EventTypes EventType { get; set; }

        [JsonPropertyName("eventTime")]
        public required string EventTime { get; set; }

        [JsonPropertyName("offline")]
        public required bool IsOffline { get; set; }

        [JsonPropertyName("metaData")]
        public string? MetaData { get; set; }
    }
}