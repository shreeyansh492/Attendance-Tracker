using System;
using System.ComponentModel;
using System.Windows;
using Tracker.ViewModels;

namespace Tracker.View
{
    /// <summary>
    /// Interaction logic for BreakWindow.xaml
    /// </summary>
    public partial class BreakWindow : Window
    {
        private readonly BreakViewModel _viewModel;
        private bool _allowClose;

        public BreakWindow(BreakViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            Closing += BreakWindow_OnClosing;
        }

        public void ShowForBreak()
        {
            _viewModel.PrepareForBreak();

            // Sync button states based on whether a break is already active
            bool isOnBreak = _viewModel.IsOnBreak;
            BreakStartButton.IsEnabled = !isOnBreak;
            BreakEndButton.IsEnabled = isOnBreak;

            // Ensure full-screen lock presentation
            WindowState = WindowState.Maximized;
            Show();
            Activate();
            Focus();
        }

        private async void BreakStartButton_OnClick(object sender, RoutedEventArgs e)
        {
            BreakStartButton.IsEnabled = false;
            BreakEndButton.IsEnabled = false;

            try
            {
                var success = await _viewModel.SubmitBreakReasonAsync();

                if (!success)
                {
                    BreakStartButton.IsEnabled = true;
                    BreakEndButton.IsEnabled = false;
                    return;
                }

                // Break started successfully -> Enable End Break button
                BreakStartButton.IsEnabled = false;
                BreakEndButton.IsEnabled = true;
            }
            catch
            {
                BreakStartButton.IsEnabled = true;
                BreakEndButton.IsEnabled = false;
            }
        }

        private async void BreakEndButton_OnClick(object sender, RoutedEventArgs e)
        {
            BreakEndButton.IsEnabled = false;

            try
            {
                await _viewModel.EndBreakAsync();

                // Hide the window after break is ended
                Hide();
            }
            catch
            {
                BreakEndButton.IsEnabled = true;
            }
        }

        private void BreakWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            // Allow exit if application is shutting down
            if (_allowClose)
                return;

            e.Cancel = true;

            // If break has ended, hide the window. If break is active, keep screen locked.
            if (_viewModel.CanCloseWindow)
            {
                Hide();
            }
            else
            {
                WindowState = WindowState.Maximized;
                Activate();
                Focus();
            }
        }

        public void AllowCloseForShutdown()
        {
            _allowClose = true;
        }
    }
}