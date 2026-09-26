using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Tracker.Configuration;

namespace Tracker.Logging
{
    public sealed class InMemoryLogSink : ILogEventSink, ILogBufferService
    {
        private readonly object _lock = new();
        private readonly Queue<string> _lines = new();
        private readonly int _limit;
        public event EventHandler? LogAdded;

        public InMemoryLogSink(DiagnosticsOptions options)
        {
            _limit = Math.Max(100, options.InMemoryLogLineLimit);
        }

        public void Emit(LogEvent logEvent)
        {
            var timestamp = logEvent.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = logEvent.Level.ToString();
            var message = logEvent.RenderMessage();

            if (logEvent.Exception is not null)
            {
                message += $" | Exception: {logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}";
            }

            Add($"{timestamp} [{level}] {message}");
        }

        public void Add(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var sanitized = Sanitize(line);

            lock (_lock)
            {
                _lines.Enqueue(sanitized);

                while (_lines.Count > _limit)
                {
                    _lines.Dequeue();
                }
                LogAdded?.Invoke(this, EventArgs.Empty);
            }
        }

        public IReadOnlyList<string> GetLatestLines()
        {
            lock (_lock)
            {
                return _lines.ToList();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _lines.Clear();
            }
        }

        private static string Sanitize(string value)
        {
            var clean = value;

            clean = MaskAfterKeyword(clean, "Authorization:");
            clean = MaskAfterKeyword(clean, "Bearer ");
            clean = MaskAfterKeyword(clean, "sessionToken");
            clean = MaskAfterKeyword(clean, "accessToken");
            clean = MaskAfterKeyword(clean, "pin");
            clean = MaskAfterKeyword(clean, "password");

            return clean;
        }

        // Fixed MED-7: Loop to ensure all occurrences on a single line are masked
        private static string MaskAfterKeyword(string value, string keyword)
        {
            int startIndex = 0;

            while ((startIndex = value.IndexOf(keyword, startIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int start = startIndex + keyword.Length;

                if (start >= value.Length)
                    break;

                int end = value.IndexOfAny([' ', ',', ';', '\r', '\n', '"', '\''], start);

                if (end < 0)
                    end = value.Length;

                value = value[..start] + " ***" + value[end..];

                // Move the index past the newly inserted mask to continue searching
                startIndex = start + 4;
            }

            return value;
        }
    }
}