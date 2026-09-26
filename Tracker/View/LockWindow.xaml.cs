using System.Windows;
using Tracker.ViewModels;

namespace Tracker.View
{
    public partial class LockWindow : Window
    {
        private readonly LockViewModel _viewModel;
        private bool _allowClose;

        public LockWindow(LockViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            Closing += LockWindow_OnClosing;
        }

        public void ShowForIdleLock(DateTimeOffset idleStart)
        {
            _viewModel.SetIdleStart(idleStart);

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

        private void LockWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;

            Activate();
            Focus();
        }
        private async void ContinueButton_OnClick(object sender,RoutedEventArgs e)
        {
            ContinueButton.IsEnabled = false;
            try
            {

                await _viewModel.OnContinueWorkingAsync();
            }
            finally
            {
                ContinueButton.IsEnabled = true;
            }
        }
    }
}