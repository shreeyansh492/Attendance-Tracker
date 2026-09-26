using System;
using Tracker.Services;

namespace Tracker.ViewModels
{
    public sealed class AutoCheckoutWarningViewModel : ViewModelBase
    {
        private string _title = "Auto Checkout Warning";
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        private string _message =
            "You have been idle for a long time. Please confirm that you are active.";
        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        private string _remainingTimeText = string.Empty;
        public string RemainingTimeText
        {
            get => _remainingTimeText;
            private set => SetProperty(ref _remainingTimeText, value);
        }

        public void SetRemainingTime(TimeSpan remainingTime)
        {
            RemainingTimeText =
                $"Auto checkout in {Math.Max(0, (int)Math.Ceiling(remainingTime.TotalMinutes))} minute(s).";
        }
    }
}
