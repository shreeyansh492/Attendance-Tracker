using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tracker.Utilities
{
    public static class JsonDefaults
    {
        public static readonly JsonSerializerOptions Web = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Web);
        }

        public static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(json, Web);
        }
    }
}
