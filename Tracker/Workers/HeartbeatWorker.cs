using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Tracker.Api;
using Tracker.Configuration;
using Tracker.Core;
using Tracker.Services;

namespace Tracker.Workers
{
    public sealed class HeartbeatWorker : BackgroundService
    {
        private readonly IApiClient _apiClient;
        private readonly ISessionService _sessionService;
        
        private readonly AttendanceOptions _attendanceOptions;
        private readonly ILogger<HeartbeatWorker> _logger;
        private readonly IAppStateService _appStateService;

        public HeartbeatWorker(
            IApiClient apiClient,
            ISessionService sessionService,
            
            AttendanceOptions attendanceOptions,
            ILogger<HeartbeatWorker> logger,
            IAppStateService appStateService)
        {
            _apiClient = apiClient;
            _sessionService = sessionService;
           
            _attendanceOptions = attendanceOptions;
            _logger = logger;
            _appStateService = appStateService;

            _attendanceOptions.Validate();
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.LogInformation("Heartbeat Worker Started. Interval Seconds={seconds}", _attendanceOptions.HeartbeatIntervalSeconds);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_attendanceOptions.HeartbeatIntervalSeconds),
                        ct);

                    await SendHeartbeatAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected heartbeat worker error.");
                }
            }

            _logger.LogInformation("Heartbeat worker stopped");
        }


        private async Task SendHeartbeatAsync(CancellationToken ct)
        {

            try
            {
                await _apiClient.SendHeartbeatAsync(ct);
                await _sessionService.UpdateLastHeartbeatAsync(ct);
            }
            catch (SessionExpiredException ex)
            {
                _logger.LogWarning(ex, "Hearbeat failed because session expired");

                await _appStateService.ForceReloginAsync("Session expired, Please login again", ct);
            }
            catch (ApiRequestException ex) when (ex.IsBusinessError)
            {
                _logger.LogWarning(ex, "Heartbeat rejected by backend. Status={Status}. body={body}", (int)ex.StatusCode, ex.ResponseBody);

            }
            catch (Exception ex) when (IsNetworkLikeFailure(ex))
            {
                _logger.LogWarning(ex, "Heartbeat Failed due to network/server issue.");
            }

        }

        private static bool IsNetworkLikeFailure(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex is TimeoutException ||
                   ex.InnerException is HttpRequestException ||
                   ex.InnerException is TaskCanceledException ||
                   ex.InnerException is TimeoutException;
        }
    }
}
