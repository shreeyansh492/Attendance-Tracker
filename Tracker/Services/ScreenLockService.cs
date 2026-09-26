using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Tracker.View;

namespace Tracker.Services
{
    public sealed class ScreenLockService : IScreenLockService
    {
        private readonly LoginWindow _loginWindow;
        private readonly LockWindow _lockWindow;
        private readonly BreakWindow _breakWindow;
        private readonly AutoCheckoutWarningWindow _autoCheckoutWarningWindow;
        private readonly ILogger<ScreenLockService> _logger;

        private readonly List<Window> _secondaryBlockers = new();

        private bool _disposed;

        public ScreenLockService(
            LoginWindow loginWindow,
            LockWindow lockWindow,
            BreakWindow breakWindow,
            AutoCheckoutWarningWindow autoCheckoutWarningWindow,
            ILogger<ScreenLockService> logger)
        {
            _loginWindow = loginWindow;
            _lockWindow = lockWindow;
            _breakWindow = breakWindow;
            _autoCheckoutWarningWindow = autoCheckoutWarningWindow;
            _logger = logger;
        }

        public void ShowLogin(string? message = null)
        {
            RunOnUiThread(() =>
            {
                _logger.LogInformation("Showing lock screen.");

                HideContentWindows();
                EnsureSecondaryBlockers();

                PreparePrimaryLockWindow(_loginWindow);
                _loginWindow.ShowForLogin(message);

                BringToFront(_loginWindow);
            });
        }

        public void ShowIdleLock(DateTimeOffset idleStart)
        {
            RunOnUiThread(() =>
            {
                _logger.LogInformation("Showing idle lock screen. IdleStartedUtc = {idleStart}", idleStart.ToUniversalTime().ToString("O"));

                HideContentWindows();
                EnsureSecondaryBlockers();

                PreparePrimaryLockWindow(_lockWindow);
                _lockWindow.ShowForIdleLock(idleStart);

                BringToFront(_lockWindow);
            });
        }

        public void ShowBreakSelection()
        {
            RunOnUiThread(() =>
            {
                _logger.LogInformation("Showing break selection screen.");
                // make sure no secondary moniter remain blocked
                CloseSecondaryBlockers();
                // Hide other lock/content windows
                SafeHide(_autoCheckoutWarningWindow);
                SafeHide(_lockWindow);
                SafeHide(_loginWindow);

                PreparePrimaryLockWindow(_breakWindow);
                _breakWindow.ShowForBreak();
                BringToFront(_breakWindow);
            });
        }

        public void ShowBreak()
        {
            RunOnUiThread(() =>
            {
                _logger.LogInformation("Showing break lock screen.");

                HideContentWindows();
                EnsureSecondaryBlockers();

                PreparePrimaryLockWindow(_breakWindow);
                _breakWindow.ShowForBreak();

                BringToFront(_breakWindow);
            });
        }

        public void ShowAutoCheckoutWarning(TimeSpan remainingTime)
        {
            RunOnUiThread(() =>
            {
                _logger.LogWarning("Showing auto-checkout warning. RemainingSeconds={Remaining}", Math.Max(0, (int)Math.Ceiling(remainingTime.TotalSeconds)));

                _autoCheckoutWarningWindow.ShowWarning(remainingTime);
                BringToFront(_autoCheckoutWarningWindow);
            });
        }

        public void Unlock()
        {
            RunOnUiThread(() =>
            {
                _logger.LogInformation("Unlocking all lock screens.");

                HideContentWindows();
                CloseSecondaryBlockers();
            });
        }

        private void HideContentWindows()
        {
            SafeHide(_autoCheckoutWarningWindow);
            SafeHide(_lockWindow);
            SafeHide(_loginWindow);
            SafeHide(_breakWindow);
        }

        private static void SafeHide(Window window)
        {
            try
            {
                if (window.IsVisible)
                {
                    window.Hide();
                }
            }
            catch
            {
                // Ignore safe hide exceptions
            }
        }

        private void EnsureSecondaryBlockers()
        {
            CloseSecondaryBlockers();

            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Primary)
                {
                    continue;
                }
                var blocker = CreateSecondaryBlocker(screen);
                blocker.Show();

                _secondaryBlockers.Add(blocker);
            }

            _logger.LogInformation("Secondary monitor blocker active. Count = {Count}", _secondaryBlockers.Count);
        }

        private static Window CreateSecondaryBlocker(Screen screen)
        {
            var blocker = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                ShowActivated = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowState = WindowState.Normal,
                AllowsTransparency = false,
                Background = System.Windows.Media.Brushes.Black,
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height
            };

            CancelEventHandler closingHandler = (_, e) =>
            {
                e.Cancel = true;
                blocker.Activate();
                blocker.Focus();
            };

            blocker.Tag = closingHandler;
            blocker.Closing += closingHandler;

            blocker.Deactivated += (_, _) =>
            {
                if (blocker.IsVisible)
                {
                    blocker.Topmost = false;
                    blocker.Topmost = true;
                    blocker.Activate();
                }
            };

            return blocker;
        }

        private void CloseSecondaryBlockers()
        {
            foreach (var blocker in _secondaryBlockers.ToList())
            {
                try
                {
                    if (blocker.Tag is CancelEventHandler handler)
                        blocker.Closing -= handler;

                    blocker.Close();
                }
                catch
                {
                    // Ignore cleanup failure.
                }
            }

            _secondaryBlockers.Clear();
        }

        private static void PreparePrimaryLockWindow(Window window)
        {
            var primary = Screen.PrimaryScreen;

            if (primary is null)
                return;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.ShowInTaskbar = false;
            window.Topmost = true;
            window.ShowActivated = true;
            window.WindowState = WindowState.Normal;

            window.Left = primary.Bounds.Left;
            window.Top = primary.Bounds.Top;
            window.Width = primary.Bounds.Width;
            window.Height = primary.Bounds.Height;
        }

        private static void BringToFront(Window window)
        {
            if (!window.IsVisible)
                window.Show();

            window.Topmost = false;
            window.Topmost = true;
            window.Activate();
            window.Focus();
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            RunOnUiThread(() =>
            {
                HideContentWindows();
                CloseSecondaryBlockers();
            });
        }
    }
}