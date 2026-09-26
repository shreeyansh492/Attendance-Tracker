using Azure;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Tracker.Configuration;
using Tracker.Core;
using Tracker.Dto;
using Tracker.Machine;
using Tracker.Security;
using Tracker.Utilities;

namespace Tracker.Api
{
    public sealed class ApiClient : IApiClient
    {
        private const string HeaderAppKey = "X-App-Key";
        private const string HeaderMachineIdentifier = "X-Machine-Identifier";

        private readonly ApiOptions _apiOptions;
        private readonly HttpClient _httpClient;
        private readonly ISecureTokenStore _tokenStore;
        private readonly IMachineIdentityService _machineIdentity;
        private readonly ILogger<ApiClient> _logger;

        public ApiClient(
            HttpClient httpClient,
            ApiOptions apiOptions,
            ISecureTokenStore tokenStore,
            IMachineIdentityService machineIdentity,
            ILogger<ApiClient> logger)
        {
            _httpClient = httpClient;
            _apiOptions = apiOptions;
            _tokenStore = tokenStore;
            _machineIdentity = machineIdentity;
            _logger = logger;

            _apiOptions.Validate();

            _httpClient.BaseAddress = BuildBaseUri(_apiOptions.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_apiOptions.TimeoutSeconds);
        }

        public async Task<bool> IsApiOnlineAsync(CancellationToken ct = default)
        {
            try
            {
                using var request = CreateRequest(
                    HttpMethod.Post, "attendance/windows/heartbeat",
                    requiresAuth: false);

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead, ct);

                var online = response.IsSuccessStatusCode;

                _logger.LogInformation(
                    "API online check result: {Online}. Status = {Status}",
                    online, (int)response.StatusCode);
                _logger.LogDebug("Heartbeat Request: {request} Response: {response}", request, response);

                return online;
            }
            catch (Exception ex) when (ex is TimeoutException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "API online check failed");
                return false;
            }
        }

        public async Task SendHeartbeatAsync(CancellationToken ct = default)
        {
            try
            {
                // Fixed LOW-3: Require authentication for the actual employee heartbeat
                using var request = CreateRequest(
                    HttpMethod.Post, "attendance/windows/heartbeat",
                    requiresAuth: true);

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead, ct);

                var online = response.IsSuccessStatusCode;

                // Fixed LOW-6: Typo in structured log property ({onlin} -> {online})
                _logger.LogInformation(
                    "Heartbeat: Status={online}, StatusCode={code}",
                    online, (int)response.StatusCode);
                _logger.LogDebug("Heartbeat Request: {request} Response: {response}", request, response);
            }
            catch (Exception ex) when (ex is TimeoutException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "Heartbeat failed.");
            }
        }

        public async Task<PinLoginResponse> PinLoginAsync(PinLoginRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Email cannot be empty.", nameof(request.Email));
            }
            if (string.IsNullOrWhiteSpace(request.Pin))
            {
                throw new ArgumentException("PIN cannot be empty.", nameof(request.Pin));
            }
            _logger.LogInformation("PIN login requested for Email: {Email}", request.Email);

            var loginResponse = await PostForDataAsync<PinLoginRequest, PinLoginResponse>(
                "attendance/windows/pin-login",
                request,
                requiresAuth: false,
                ct);

            _tokenStore.SaveAccessToken(loginResponse.SessionToken);
            _logger.LogDebug("PinLogin Request: {request} Response: {response}", request, loginResponse);
            _logger.LogInformation("PIN login succeeded for {Email}. EmployeeId = {EmployeeId}", loginResponse.Email, loginResponse.EmployeeId);

            return loginResponse;
        }

        public async Task SendAttendanceEventAsync(List<AttendanceEventRequest> request, CancellationToken ct = default)
        {
            await PostForDataAsync<List<AttendanceEventRequest>, object>(
                "attendance/windows/event",
                request,
                requiresAuth: true,
                ct);
        }

        private async Task<TResponse> PostForDataAsync<TRequest, TResponse>(
            string path, TRequest body, bool requiresAuth, CancellationToken ct)
        {
            var responseBody = await SendAsync(path, body, requiresAuth, ct);

            // Fixed HIGH-5: Removed the short-circuit for typeof(TResponse) == typeof(object)
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return default!;
            }

            var apiResponse = DeserializeApiResponse<TResponse>(responseBody, path);

            // Allow Data to be null only if we requested an object (e.g. ApiResponseVoid), but STILL enforce the success check.
            if (apiResponse.Data is null && typeof(TResponse) != typeof(object))
            {
                if (apiResponse.Success)
                {
                    return default!;
                }

                throw new ApiRequestException($"Request to {path} failed.", HttpStatusCode.BadRequest, responseBody);
            }

            return apiResponse.Data!;
        }

        private async Task<string> SendAsync<TRequest>(
            string path, TRequest body, bool requiresAuth, CancellationToken ct)
        {
            using var request = CreateJsonPostRequest(path, body, requiresAuth);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

            return await HandleResponseAsync(response, path, ct);
        }

        private async Task<string> HandleResponseAsync(
            HttpResponseMessage response,
            string path,
            CancellationToken ct)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _tokenStore.ClearAccessToken();
                _logger.LogWarning("HTTP 401 on {Path}. Body = {Body}", path, responseBody);

                throw new SessionExpiredException("Session expired. Please login again.");
            }
            if (response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Forbidden ||
                (int)response.StatusCode == 422)
            {
                _logger.LogWarning("Business/API error on {Path}. Status = {Status}. Body = {Body}", path, (int)response.StatusCode, responseBody);

                throw new ApiRequestException($"Request to {path} failed.", response.StatusCode, responseBody);
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Business/API error on {Path}. Status = {Status}. Body = {Body}", path, (int)response.StatusCode, responseBody);

                throw new HttpRequestException($"Request to {path} failed with HTTP {(int)response.StatusCode}");
            }

            return responseBody;
        }

        private HttpRequestMessage CreateJsonPostRequest<TRequest>(
            string path, TRequest body, bool requiresAuth)
        {
            var request = CreateRequest(HttpMethod.Post, path, requiresAuth);
            var json = JsonSerializer.Serialize(body, JsonDefaults.Web);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return request;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool requiresAuth)
        {
            var request = new HttpRequestMessage(method, NormalizeRelativePath(path));

            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation(HeaderAppKey, _apiOptions.WindowsAppKey);
            request.Headers.TryAddWithoutValidation(HeaderMachineIdentifier, _machineIdentity.MachineIdentifier);

            if (requiresAuth)
            {
                var accessToken = _tokenStore.GetAccessToken();
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                }
            }
            return request;
        }

        private static ApiResponse<T> DeserializeApiResponse<T>(
            string responseBody, string path)
        {
            try
            {
                var result = JsonSerializer.Deserialize<ApiResponse<T>>(responseBody, JsonDefaults.Web);

                if (result is null)
                {
                    throw new ApiRequestException($"Empty API response from {path}.", HttpStatusCode.BadRequest, responseBody);
                }
                if (result.Failed)
                {
                    throw new ApiRequestException(result.Message ?? $"API returned failed response for {path}.", HttpStatusCode.BadRequest, responseBody);
                }
                return result;
            }
            catch (JsonException ex)
            {
                throw new ApiRequestException($"Invalid JSON response from {path}.", HttpStatusCode.BadRequest, responseBody, ex);
            }
        }

        private static Uri BuildBaseUri(string baseUrl)
        {
            var normalized = baseUrl.Trim();

            if (!normalized.EndsWith("/", StringComparison.Ordinal))
            {
                normalized += "/";
            }

            return new Uri(normalized, UriKind.Absolute);
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.TrimStart('/');
        }
    }
}