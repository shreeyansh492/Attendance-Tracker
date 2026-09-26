using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Tracker.Dto
{
    public sealed class PinLoginRequest
    {
        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("pin")]
        public required string Pin {  get; set; }

        [JsonPropertyName("machineIdentifier")]
        public required string MachineIdentifier { get; set; }

        [JsonPropertyName("machineName")]
        public required string MachineName { get; set; }
    }
}
