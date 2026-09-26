using System;
using System.Collections.Generic;
using System.Text;
using Tracker.Data.Entity;
using Tracker.Dto;

namespace Tracker.Services
{
    public interface IEmployeeService
    {
        EmployeeEntity? Employee { get; }

        Task UpdateOrCreateEmployeeRecordAfterLoginAsync(PinLoginResponse response,string email, string pin, CancellationToken ct = default);

        Task<EmployeeEntity?> GetEmployeeRecordAsync(string email, CancellationToken ct = default);

        Task<EmployeeEntity?> GetEmployeeBySessionIdAsync(Guid id, CancellationToken ct = default);

        Task DeleteOldRecordAsync(string email, CancellationToken ct = default);
    }
}
