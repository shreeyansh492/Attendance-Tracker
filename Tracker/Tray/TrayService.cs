using System.IO;
using System;
using System.Drawing;
using System.Windows.Forms;
using Tracker.Data.Entity;
using Tracker.Services;
using Tracker.View;

namespace Tracker.Tray
{
    public sealed class TrayService : ITrayService
    {
        private readonly IAppStateService _appStateService;
        private readonly TrayStatusWindow _trayStatusWindow;
        private readonly DiagnosticsWindow _diagnosticsWindow;
        private readonly BreakWindow _breakWindow; 
        private ToolStripMenuItem? _manualCheckoutItem;
        private NotifyIcon? _notifyIcon;

        public TrayService(IAppStateService appStateService, TrayStatusWindow trayStatusWindow, BreakWindow breakWindow, DiagnosticsWindow diagnosticsWindow)
        {
            _appStateService = appStateService;
            _trayStatusWindow = trayStatusWindow;
            _diagnosticsWindow = diagnosticsWindow;
            _breakWindow = breakWindow;
            _appStateService.StateChanged += OnAppStateChanged;
        }

        private void OnAppStateChanged(object? sender, AppStateChangedEventArgs e)
        {
            UpdateManualCheckoutState();
        }

        private void UpdateManualCheckoutState()
        {
            if (_manualCheckoutItem is null)
            {
                return;
            }

            var state = _appStateService.CurrentState;
            _manualCheckoutItem.Enabled = state == AppState.Working || state == AppState.IdleLocked;
        }

        public void Initialize()
        {
            var contextMenu = new ContextMenuStrip();

            _manualCheckoutItem = new ToolStripMenuItem("Manual Checkout");

            _manualCheckoutItem.Click += async (_, _) =>
            {
                var result = System.Windows.MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                     System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }

                try
                {
                    await _appStateService.OnManualCheckoutAsync();
                }
                catch (Exception ex)
                {
                    // FIX: Actually display the error to the user instead of swallowing it silently
                    System.Windows.MessageBox.Show($"Manual checkout failed: {ex.Message}", "Checkout Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Manual checkout failed: {ex}");
                }
            };

            contextMenu.Items.Add(_manualCheckoutItem);

            var breakItem = new ToolStripMenuItem("Start Break");
            breakItem.Click += (_, _) =>
            {
                _breakWindow.ShowForBreak();
                _breakWindow.Focus();
            };
            contextMenu.Items.Add(breakItem);

            var diagnosticsItem = new ToolStripMenuItem("Diagnostics");
            diagnosticsItem.Click += (_, _) =>
            {
                _diagnosticsWindow.Show();
                _diagnosticsWindow.Activate();
                _diagnosticsWindow.Focus();
            };
            contextMenu.Items.Add(diagnosticsItem);

            var showStatusItem = new ToolStripMenuItem("Show Status");
            showStatusItem.Click += (_, _) =>
            {
                _trayStatusWindow.RefreshEmployee();
                _trayStatusWindow.Show();
                _trayStatusWindow.Activate();
                _trayStatusWindow.Focus();
            };
            contextMenu.Items.Add(showStatusItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(System.IO.Path.Combine( AppContext.BaseDirectory,"Assets","Logo.ico")),
                Visible = true,
                Text = "Tracker",
                ContextMenuStrip = contextMenu
            };

            _notifyIcon.DoubleClick += (_, _) =>
            {
                _trayStatusWindow.Show();
                _trayStatusWindow.Activate();
                _trayStatusWindow.Focus();
            };
        }

        public void Dispose()
        {
            _appStateService.StateChanged -= OnAppStateChanged;
            if (_notifyIcon is null)
            {
                return;

            }

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}