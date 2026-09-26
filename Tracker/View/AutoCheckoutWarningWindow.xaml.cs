using System;
using System.Windows;
using Tracker.Services;
using Tracker.ViewModels;

namespace Tracker.View
{
    public partial class AutoCheckoutWarningWindow : Window
    {
        private readonly IAppStateService _appStateService;
        private bool _allowClose;
        private bool _allowUserClose;

        public AutoCheckoutWarningWindow(
            IAppStateService appStateService, AutoCheckoutWarningViewModel viewModel)
        {
            InitializeComponent();

            _appStateService = appStateService;
            DataContext = viewModel;
            Closing += AutoCheckoutWarningWindow_OnClosing;
        }

        private async void ContinueWorkingButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Fixed CRIT-4: Wire up the correct acknowledgement method instead of unlocking the screen directly
            await _appStateService.OnAutoCheckoutWarningAcknowledgedAsync();
            _allowUserClose = true;
            Close();
        }

        public void ShowWarning(TimeSpan remainingTime)
        {
            if (DataContext is AutoCheckoutWarningViewModel viewModel)
            {
                viewModel.SetRemainingTime(remainingTime);
            }

            if (!IsVisible)
            {
                Show();
            }

            Activate();
            Focus();
        }

        public void AllowCloseForShutdown()
        {
            _allowClose = true;
        }

        private void AutoCheckoutWarningWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose || _allowUserClose)
            {
                return;
            }

            // User should not close the warning window manually.
            e.Cancel = true;

            Activate();
            Focus();
        }
    }
}