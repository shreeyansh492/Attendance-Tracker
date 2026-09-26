using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Api;
using Tracker.Data;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Services
{
    public sealed class EmployeeService : IEmployeeService
    {
        private readonly IAppRepository _appRepository;
        private readonly IClockService _clockService;
        private readonly ILogger<EmployeeService> _logger;

        public EmployeeService(
            IAppRepository appRepository,
            IClockService clockService,
            ILogger<EmployeeService> logger)
        {
            _appRepository = appRepository;
            _clockService = clockService;
            _logger = logger;
        }

        public EmployeeEntity? Employee { get; private set; }

        public async Task UpdateOrCreateEmployeeRecordAfterLoginAsync(PinLoginResponse response, string email, string pin, CancellationToken ct = default)
        {
            try
            {
                var employee = await _appRepository.GetEmployeeByEmailAsync(email, ct);
                var now = _clockService.UtcNow;

                if (employee is null)
                {
                    employee = new EmployeeEntity
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        PinHash = PinHasher.Hash(pin),
                        EmployeeCode = response.EmployeeCode,
                        EmployeeId = response.EmployeeId,
                        EmployeeName = response.EmployeeName,
                        CreatedAt = now,
                        Department = response.Department,
                        Designation = response.Designation,
                        Version = 0,
                        CreatedBy = email,
                        UpdatedAt = now,
                        UpdatedBy = email
                    };
                }
                else
                {
                    bool isSamePin = PinHasher.Verify(pin, employee.PinHash);
                    if (!isSamePin)
                    {
                        employee.PinHash = PinHasher.Hash(pin);
                    }
                    employee.EmployeeId = response.EmployeeId;
                    employee.EmployeeName = response.EmployeeName;
                    employee.Department = response.Department;
                    employee.Designation = response.Designation;
                    employee.UpdatedAt = now;
                    employee.UpdatedBy = email;
                    employee.Version++;
                }

                await _appRepository.SaveEmployeeAsync(employee, ct);
                Employee = employee;
                _logger.LogInformation(
    "LOGIN RESPONSE => Name={Name}, Department={Department}, Designation={Designation}",
    response.EmployeeName,
    response.Department,
    response.Designation);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("An error occurred while updating/saving employee record for {Email}, Error = {Error}", email, ex.Message);
                throw;
            }
        }

        public async Task<EmployeeEntity?> GetEmployeeRecordAsync(string email, CancellationToken ct = default)
        {
            return await _appRepository.GetEmployeeByEmailAsync(email, ct);
        }

        public async Task DeleteOldRecordAsync(string email, CancellationToken ct = default)
        {
            await _appRepository.DeleteEmployeeRecordAsync(email, ct);
        }

        public async Task<EmployeeEntity?> GetEmployeeBySessionIdAsync(Guid id, CancellationToken ct = default)
        {
            try
            {
                var employee = await _appRepository.GetEmployeeBySessionIdAsync(id, ct);
                if (employee is null)
                {
                    _logger.LogWarning("No employee found from sessionId={sessionId}", id);
                    return null;
                }
                return employee;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch employee from session {sessionId}", id);
                throw;
            }
        }
    }
}