using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Data.Entity
{
    public sealed class EmployeeEntity : BaseEntity
    {
        public Guid EmployeeId { get; set; }

        public string? EmployeeCode { get; set; } = string.Empty;

        public string? EmployeeName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PinHash { get; set; } = string.Empty;

        public string? Department { get; set; } = string.Empty;

        public string? Designation {  get; set; } = string.Empty;
    }
}
