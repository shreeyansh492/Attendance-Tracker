using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Api;
using Tracker.Core;
using Tracker.Dto;
using Tracker.Machine;

namespace Tracker.Services
{
    public sealed class AuthService : IAuthService
    {
        private readonly IApiClient _apiClient;
        private readonly IMachineIdentityService _machineIdentityService;
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            ILogger<AuthService> logger,
            IApiClient apiClient,
            IMachineIdentityService machineIdentityService,
            IEmployeeService employeeService)
        {
            _apiClient = apiClient;
            _machineIdentityService = machineIdentityService;
            _employeeService = employeeService;
            _logger = logger;
        }

        public async Task<PinLoginResponse?> LoginAsync(string email, string pin, CancellationToken ct)
        {
            var machineIdentifier = _machineIdentityService.MachineIdentifier;
            var machineName = _machineIdentityService.MachineName;
            var request = new PinLoginRequest
            {
                Email = email,
                Pin = pin,
                MachineIdentifier = machineIdentifier,
                MachineName = machineName
            };

            var isOnline = await _apiClient.IsApiOnlineAsync(ct);
            PinLoginResponse? loginResponse = null;

            if (isOnline)
            {
                try
                {
                    loginResponse = await _apiClient.PinLoginAsync(request, ct);

                    if (loginResponse is null)
                    {
                        throw new AuthException("Invalid Email or PIN");
                    }

                    await _employeeService.UpdateOrCreateEmployeeRecordAfterLoginAsync(loginResponse, email, pin, ct);
                    return loginResponse;
                }
                catch (ApiRequestException ex) when (
                    ex.StatusCode == HttpStatusCode.BadRequest ||
                    ex.StatusCode == HttpStatusCode.Unauthorized ||
                    ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("Online Authentication Rejected By Server. {StatusCode} Message = {Message}", (int)ex.StatusCode, ex.Message);
                    throw new AuthException("Invalid Email or PIN.");
                }
                catch (AuthException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Online Authentication Failed. Message = {Message}", ex.Message);
                }
            }

            if (loginResponse is null)
            {
                _logger.LogWarning("Proceeding with offline login.");

                try
                {
                    var employee = await _employeeService.GetEmployeeRecordAsync(email, ct);

                    if (employee is null)
                    {
                        throw new AuthException("No offline record found. Connect to the network to log in.");
                    }

                    if (employee.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-4))
                    {
                        await _employeeService.DeleteOldRecordAsync(email, ct);
                        throw new AuthException("Offline Records Expired. Connect to the network to log in.");
                    }

                    if (!PinHasher.Verify(pin, employee.PinHash))
                    {
                        throw new AuthException("Invalid PIN");
                    }

                    _logger.LogInformation("Offline Login Succeeded.");

                   
                    // FIX: Must map to EmployeeId (the backend GUID), NOT Id (the local SQLite key)
                    loginResponse = new PinLoginResponse
                    {
                        EmployeeId = employee.EmployeeId,
                        EmployeeCode = employee.EmployeeCode,
                        Email = email,
                        EmployeeName = employee.EmployeeName,
                        Department = employee.Department,
                        Designation = employee.Designation,
                        SessionToken = string.Empty,
                        SessionExpiresInMs = 0
                    };
                }
                catch (AuthException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Got into problem while trying offline login. Error = {Error}", ex.Message);
                    throw new AuthException("An unexpected error occurred.");
                }
            }

            return loginResponse;
        }
    }
}