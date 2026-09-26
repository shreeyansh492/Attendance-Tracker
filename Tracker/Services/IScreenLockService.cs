using System;

namespace Tracker.Services
{
    public interface IScreenLockService
    {
        void ShowLogin(string? message = null);
        void ShowIdleLock(DateTimeOffset idleStart);
        void ShowAutoCheckoutWarning(TimeSpan remainingTime);
        void ShowBreakSelection();
        void ShowBreak();
        void Unlock();
        void Dispose();
    }
}