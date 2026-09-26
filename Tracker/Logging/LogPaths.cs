using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tracker.Configuration;

namespace Tracker.Logging
{
    public sealed class LogPaths
    {
        private readonly StorageOptions _storageOptions;

        public LogPaths(StorageOptions storageOptions)
        {
            _storageOptions = storageOptions;
        }

        public string LogsDirectory
        {
            get
            {
                var directory = Path.Combine(
                    _storageOptions.GetAppDataDirectory(),
                    "Logs");

                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public string CurrentLogFilePath
        {
            get
            {
                return Path.Combine(
                    LogsDirectory,
                    "agent-.log");
            }
        }

        public string DiagnosticsExportDirectory
        {
            get
            {
                var directory = Path.Combine(
                    _storageOptions.GetAppDataDirectory(),
                    "Diagnostics");

                Directory.CreateDirectory(directory);

                return directory;
            }
        }
    }
}
