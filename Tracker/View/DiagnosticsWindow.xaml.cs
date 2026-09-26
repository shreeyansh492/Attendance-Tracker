using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Tracker.ViewModels;
using System.Collections.Specialized;

namespace Tracker.View
{
    /// <summary>
    /// Interaction logic for DiagnosticsWindow.xaml
    /// </summary>
    public partial class DiagnosticsWindow : Window
    {
        private readonly DiagnosticsViewModel _viewModel;
        private bool _allowClose;

        public DiagnosticsWindow(DiagnosticsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.LogLines.CollectionChanged += LogLines_CollectionChanged;

      Closing += DiagnosticsWindow_OnClosing;
           Loaded += DiagnosticsWindow_OnLoaded;
        }

        public void AllowCloseForShutdown()
        {
            _allowClose = true;
        }

        private void DiagnosticsWindow_OnClosing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;

            Hide();
        }

        private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshAsync();

            if (AutoScrollCheck.IsChecked == true &&
                LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(
                    LogListBox.Items[LogListBox.Items.Count - 1]);
            }
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearLogs();
        }

        private void LogLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (AutoScrollCheck.IsChecked != true ||
                LogListBox.Items.Count == 0)
            {
                return;
            }

            LogListBox.ScrollIntoView(
                LogListBox.Items[LogListBox.Items.Count - 1]);
        }

        private async void DiagnosticsWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshAsync();
        }
    }
}