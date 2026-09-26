using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Tracker.Dto
{
    public sealed class ApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("data")]
        public T? Data { get; init; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }

        public bool Failed => !Success;
    }

    public sealed class ApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }

        public bool Failed => !Success;
    }
}
