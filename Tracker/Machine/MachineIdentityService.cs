using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace Tracker.Machine
{
    public sealed class MachineIdentityService : IMachineIdentityService
    {
        private readonly ILogger<MachineIdentityService> _logger;
        private readonly Lazy<string> _machineIdentifier;

        public MachineIdentityService(ILogger<MachineIdentityService> logger)
        {
            _logger = logger;
            _machineIdentifier = new Lazy<string>(BuildMachineIdentifier);
        }

        public string MachineIdentifier => _machineIdentifier.Value;

        public string MachineName => Environment.MachineName;

        private string BuildMachineIdentifier()
        {
            var rawIdentity = TryGetBestMacAddress();
            if (string.IsNullOrWhiteSpace(rawIdentity))
            {
                rawIdentity = Environment.MachineName;
                _logger.LogWarning("No Active Mac address found. Falling back to machine name for machine identity.");

            }
            var normalized = rawIdentity.Trim().ToUpperInvariant();
            var hash = Sha256(normalized);
            var machineId = $"TWC-{hash[..16]}";

            _logger.LogInformation("Machine Identity generated. MachineName = {MachineName}, MachineIdentifier = {machineId}", MachineName, machineId);

            return machineId;
        }

        private static string? TryGetBestMacAddress()
        {
            try
            {
                // Fixed HIGH-6: Changed '==' to '!=' for Tunnel interfaces
                var candidates = NetworkInterface.GetAllNetworkInterfaces().Where(
                    nic => nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(
                    nic => new { Nic = nic, Mac = nic.GetPhysicalAddress()?.ToString() })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Mac))
                    .OrderByDescending(x => x.Nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .ThenByDescending(x => x.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .ThenBy(x => x.Nic.Name)
                    .FirstOrDefault();

                return candidates?.Mac;
            }
            catch
            {
                return null;
            }
        }

        private static string Sha256(string mac)
        {
            var bytes = Encoding.UTF8.GetBytes(mac);
            var hashBytes = SHA256.HashData(bytes);

            return Convert.ToHexString(hashBytes);
        }
    }
}