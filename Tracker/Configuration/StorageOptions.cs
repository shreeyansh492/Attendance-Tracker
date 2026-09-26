using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tracker.Configuration
{
    public sealed class StorageOptions
    {
        public const string SectionName = "Storage";

        public string AppDataFolderName { get; set; } = "TrackerAgent";

        public string DatabaseFileName { get; set; } = "attendance.db";

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(AppDataFolderName))
            {
                throw new InvalidOperationException("Storage: AppDataFolderName is missing in appsettings.json");
            }
            if (string.IsNullOrWhiteSpace(DatabaseFileName))
            {
                throw new InvalidOperationException("Storage: DatabaseFileName is missing in appsettings.json");
            }
            if (!DatabaseFileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Storage: DatabaseFileName must end with .db ");
            }
        }

        public string GetAppDataDirectory()
        {
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var directory = Path.Combine(roamingAppData, AppDataFolderName);

            Directory.CreateDirectory(directory);

            return directory;
        }

        public string GetDatabasePath()
        {
            var directory = GetAppDataDirectory();

            return Path.Combine(directory, DatabaseFileName);
        }

        public string GetSqliteConnectionString()
        {
            return $"Data Source={GetDatabasePath()}";
        }
    }
}
