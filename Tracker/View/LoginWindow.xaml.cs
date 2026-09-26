using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tracker.ViewModels;

namespace Tracker.View
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {

        private readonly LoginViewModel _viewModel;
        private bool _allowClose;
        private bool _isLoggingIn;
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            Closing += LoginWindow_OnClosing;
            if (PinPasswordBox is not null)
            {
                PinPasswordBox.PreviewKeyDown += PinPasswordBox_PreviewKeyDown_PreventPaste;
                PinPasswordBox.MaxLength = 4;
            }
        }

        public void ShowForLogin(string? message = null)
        {
            _viewModel.PrepareForLogin(message);

            var passwordBox = FindVisualChild<PasswordBox>(this);
            passwordBox?.Clear();

            Show();
            Activate();
            Focus();

            passwordBox?.Focus();
        }

        private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
        {
            await TryLoginAsync();
        }

        public async void PinPasswordBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }
            e.Handled = true;

            await TryLoginAsync();
        }

        private async Task TryLoginAsync()
        {
            if (_isLoggingIn)
            {
                return;
            }
            if(LoginButton.IsEnabled == false)
            {
                return;
            }
            _isLoggingIn = true;
            LoginButton.IsEnabled = false;
            try
            {
                var passwordBox = PinPasswordBox ?? FindVisualChild<PasswordBox>(this);
                var pin = passwordBox?.Password ?? string.Empty;

                if (EmailTextBox is not null)
                {
                    var email = EmailTextBox.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
                    {
                        System.Windows.MessageBox.Show(this, "Please enter a valid email address.", "Invalid Email", MessageBoxButton.OK, MessageBoxImage.Error);
                        EmailTextBox.Focus();
                        return;
                    }
                }

                var success = await _viewModel.LoginSuccessAsync(pin, CancellationToken.None);
                if (!success)
                {
                    System.Windows.MessageBox.Show(this, "Login Failed. Please Check your credentials and try again.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                passwordBox?.Clear();
                Hide();
            }
            finally
            {
                _isLoggingIn = false;
                LoginButton.IsEnabled = true;
            }
        }

        private void LoginWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }
            e.Cancel = true;
            Hide();
        }

        public void AllowCloseForShutdown()
        {
            _allowClose = true;
        }

        private static T? FindVisualChild<T>( DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if(child is T typedChild)
                {
                    return typedChild;
                }
                var result = FindVisualChild<T>(child);
                if(result is not null)
                {
                    return result;
                }
            }
            return null;
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show("Your Attendance will not be recorded. Are you sure you want to exit?", "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void EmailTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            PinPasswordBox.Focus();
        }

        private void PinPasswordBox_PreviewKeyDown_PreventPaste(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if((e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ||
                (e.Key == Key.Insert && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift))
            {
                e.Handled = true;
            }
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }
            try
            {
                return Regex.IsMatch(email,
                "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250));
            }
            catch
            {
                return false;
            }
        }
    }
}
