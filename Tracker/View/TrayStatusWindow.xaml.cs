using System;
using System.Windows;
using Tracker.ViewModels;

namespace Tracker.View
{
    /// <summary>
    /// Interaction logic for TrayStatusWindow.xaml
    /// </summary>
    public partial class TrayStatusWindow : Window
    {
        private readonly TrayStatusViewModel _viewModel;
        private bool _allowClose;

        public TrayStatusWindow(TrayStatusViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            Closing += TrayStatusWindow_OnClosing;
            _viewModel.LoadEmployee();

        }

        public void AllowCloseForShutdown()
        {
            _allowClose = true;
        }
        public void RefreshEmployee()
        {
            _viewModel.LoadEmployee();
        }

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            _viewModel.LoadEmployee();
        }

        private void TrayStatusWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;

            // Fixed: Hide the window to the tray instead of forcing it to stay open and stealing focus
            Hide();
        }
    }
}