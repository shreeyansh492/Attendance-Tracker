using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection; // Added for GetRequiredService
using Tracker.Api;
using Tracker.Services;
using Tracker.Dto;
using Tracker.Machine;
using Tracker.Core;
using Tracker.Data.Entity;

namespace Tracker.ViewModels
{
    public sealed partial class LoginViewModel : ViewModelBase
    {
        private readonly ILogger<LoginViewModel> _logger;
        private readonly IAuthService _authService;
        private readonly IAppStateService _appStateService;
        private readonly ISessionService _sessionService;
        private readonly IClockService _clock;
        private readonly IEventService _eventService;
        private readonly IServiceProvider _serviceProvider; // Replaced IScreenLockService

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _title = "WizeVision";

        [ObservableProperty]
        private string _subtitle = "Login with your company email and PIN.";

        public LoginViewModel(
            ILogger<LoginViewModel> logger,
            IAuthService authService,
            IEventService eventService,
            IAppStateService appStateService,
            IClockService clock,
            ISessionService sessionService,
            IServiceProvider serviceProvider) // Inject the provider instead
        {
            _logger = logger;
            _authService = authService;
            _sessionService = sessionService;
            _clock = clock;
            _appStateService = appStateService;
            _eventService = eventService;
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> LoginSuccessAsync(string pin, CancellationToken ct = default)
        {
            if (IsBusy)
            {
                return false;
            }
            ClearMessage();

            var normalizedEmail = Email.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                SetError("Please enter your Email.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(pin))
            {
                SetError("Please enter your PIN.");
                return false;
            }

            if (pin.Length != 4 || !pin.All(char.IsDigit))
            {
                SetError("PIN must be exactly 4 digits.");
                return false;
            }

            IsBusy = true;
            SetStatus("Logging in...");

            try
            {
                var response = await _authService.LoginAsync(normalizedEmail, pin, ct);

                if (response is null)
                {
                    SetError("Authentication failed. Please try again.");
                    return false;
                }

                SetStatus("Login successful!");

                var currentSession = await _sessionService.LoadOrCreateSessionAfterLoginAsync(response, ct);

                await _eventService.AutoCheckinAsync(currentSession.Id, normalizedEmail, ct);

                // ==========================================
                // NEW BACKGROUND SYNC INTEGRATION
                // ==========================================
                // Fire and forget the background sync using the fresh token
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Use CancellationToken.None so the sync completes even if the login UI context closes
                        await _eventService.SyncPendingEventsAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Fire-and-forget offline sync encountered an unexpected error.");
                    }
                });
                // ==========================================

                _appStateService.TransitionTo(AppState.Working);

                // Resolve the service dynamically here to break the circular dependency
                var screenLockService = _serviceProvider.GetRequiredService<IScreenLockService>();
                screenLockService.Unlock();

                _logger.LogInformation("Login complete for {Email}", normalizedEmail);
                return true;
            }
            catch (AuthException ex)
            {
                SetError(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading the session.");
                SetError("A connection or system error occurred.");
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void PrepareForLogin(string? message = null)
        {
            ClearMessage();

            if (!string.IsNullOrWhiteSpace(message))
            {
                SetStatus(message);
            }
        }

        public void SetStatusExternal(string message)
        {
            SetStatus(message);
        }
    }
}