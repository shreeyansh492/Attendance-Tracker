using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Tracker.Configuration;

namespace Tracker.Security
{
    public sealed class DpapiSecureTokenStore : ISecureTokenStore
    {
        private const string AccessTokenFileName = "access_token.dat";

        private readonly StorageOptions _storageOptions;

        public DpapiSecureTokenStore(StorageOptions storageOptions)
        {
            _storageOptions = storageOptions;
        }

        public bool HasAccessToken => !string.IsNullOrWhiteSpace(GetAccessToken());

        public void SaveAccessToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                // Fixed LOW-1: Corrected the order of ArgumentNullException parameters
                throw new ArgumentNullException(nameof(accessToken), "Access token cannot be empty.");
            }
            SaveProtectedText(GetAccessTokenPath(), accessToken);
        }

        public string? GetAccessToken()
        {
            return LoadProtectedText(GetAccessTokenPath());
        }

        public void ClearAccessToken()
        {
            DeleteFileIfExists(GetAccessTokenPath());
        }

        private string GetAccessTokenPath()
        {
            return Path.Combine(
                _storageOptions.GetAppDataDirectory(),
                AccessTokenFileName);
        }

        private static void SaveProtectedText(
            string fielPath, string value)
        {
            var plainBytes = Encoding.UTF8.GetBytes(value);
            var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);

            File.WriteAllBytes(fielPath, protectedBytes);
        }

        private static string? LoadProtectedText(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var protectedBytes = File.ReadAllBytes(filePath);
                var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                DeleteFileIfExists(filePath);
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void DeleteFileIfExists(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }
            try
            {
                File.Delete(filePath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}