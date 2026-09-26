using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tracker.ViewModels
{
    public abstract partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string? _statusMessage;

        protected void SetError(string message)
        {
            ErrorMessage = message;
            StatusMessage = null;
        }

        protected void SetStatus(string status)
        {
            StatusMessage = status;
            ErrorMessage = null;
        }

        protected void ClearMessage()
        {
            ErrorMessage = null;
            StatusMessage = null;
        }
    }
}
