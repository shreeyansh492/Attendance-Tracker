using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Configuration;
using Tracker.Data;

namespace Tracker.Workers
{
    public sealed class CleanupWorker : BackgroundService
    {
        private readonly ILogger<CleanupWorker> _logger;
        private readonly IAppRepository _appRepository;
        private readonly AttendanceOptions _options;

        public CleanupWorker(
            ILogger<CleanupWorker> logger,
            IAppRepository appRepository,
            AttendanceOptions options)
        {
            _logger = logger;
            _appRepository = appRepository;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cleanup worker started.");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var now = DateTimeOffset.UtcNow;

                        // Fixed MED-1 & MED-3: Read the retention window from config so it aligns with offline login rules
                        var cutoff = now.AddDays(-_options.DeleteRecordAfterDays);

                        await _appRepository.DeleteOldSyncedEventsAsync(cutoff, stoppingToken);
                        await _appRepository.DeleteOldEmployeesAsync(cutoff, stoppingToken);
                        await _appRepository.DeleteOldClosedSessionsAsync(cutoff, stoppingToken);

                        _logger.LogInformation(
                            "Cleanup completed. Cutoff={Cutoff}", cutoff);

                        // Task.Delay will throw TaskCanceledException when app closes
                        await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Only log real errors (database failures, network, etc.)
                        _logger.LogError(ex, "Error in cleanup worker. Retrying in 10 minutes.");
                        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when stoppingToken is canceled during Task.Delay
                return;
            }

            _logger.LogInformation("Cleanup worker stopped.");
        }
    }
}