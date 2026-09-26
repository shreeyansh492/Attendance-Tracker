using System;
using Tracker.Data.Entity;
using Tracker.Services;

namespace Tracker.ViewModels
{
    public sealed class TrayStatusViewModel : ViewModelBase
    {
        private readonly IAppStateService _appStateService;
        private readonly IEmployeeService _employeeService;

        public TrayStatusViewModel(IAppStateService appStateService, IEmployeeService employeeService)
        {
            _appStateService = appStateService;
            _employeeService = employeeService;

            _appStateService.StateChanged += OnStateChanged;
        }

        public string StateText =>
            _appStateService.CurrentState switch
            {
                AppState.Working => "Working",
                AppState.IdleLocked => "Idle",
                AppState.OnBreak => "On Break",
                AppState.CheckedOut => "Checked Out",
                AppState.LoggedOut => "Logged Out",
                _ => "Starting"
            };

        public bool IsWorking =>
            _appStateService.CurrentState == AppState.Working;

        public bool IsIdle =>
            _appStateService.CurrentState == AppState.IdleLocked;

        public bool IsOnBreak =>
            _appStateService.CurrentState == AppState.OnBreak;
        public string EmployeeText { get => _employeeText; private set => SetProperty(ref _employeeText, value); }

        public string DesignationText { get => _designationText; private set => SetProperty(ref _designationText, value); }
        public string DepartmentText { get => _departmentText; private set => SetProperty(ref _departmentText, value); }
        private string _employeeText = "Not available";     
        private string _designationText = "Not available";
        private string _departmentText = "Not available";
       

        private void OnStateChanged(
            object? sender,
            AppStateChangedEventArgs e)
        {
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(IsWorking));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsOnBreak));
        }
        public void LoadEmployee()
        {
            try
            {
                var employee = _employeeService.Employee;

                if (employee is null)
                {
                    EmployeeText = "Not available";
                    DesignationText = "Not available";
                    DepartmentText = "Not available";
                    ErrorMessage = "Employee information is not available.";
                    return;
                }

                EmployeeText = employee.EmployeeName ?? "Not available";
                DesignationText = employee.Designation ?? "Not available";
                DepartmentText = employee.Department ?? "Not available";
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                EmployeeText = "Not available";
                DesignationText = "Not available";
                DepartmentText = "Not available";
                ErrorMessage = $"Unable to load employee information: {ex.Message}";
            }
        }
    }
}