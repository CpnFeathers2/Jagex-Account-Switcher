using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace JagexAccountSwitcher.Helpers
{
    public static class CredentialsHelper
    {
        /// <summary>
        /// DEPRECATED: Use account-specific credential files instead
        /// This method is kept for backward compatibility only
        /// </summary>
        [Obsolete("Use WriteCredentialsToFile(accountId, email, password) instead")]
        public static void WriteCredentials(string email, string password)
        {
            // Legacy implementation - writes to shared file (DO NOT USE)
            string path = Path.Combine(Directory.GetCurrentDirectory(), "last_login.txt");
            File.WriteAllText(path, $"{email}:{password}");
        }

        /// <summary>
        /// Writes credentials to an account-specific file in an isolated directory
        /// This prevents credential collision when launching multiple accounts
        /// </summary>
        public static string WriteCredentialsToFile(string accountId, string email, string password)
        {
            try
            {
                // Create account-specific workspace
                string workspaceDir = Path.Combine(Directory.GetCurrentDirectory(), "AccountWorkspaces", accountId);
                Directory.CreateDirectory(workspaceDir);

                // Write credentials to account-specific file
                string credFile = Path.Combine(workspaceDir, "credentials.txt");
                File.WriteAllText(credFile, $"{email}:{password}");

                Console.WriteLine($"[CredentialsHelper] Wrote credentials for {accountId} to {credFile}");
                return credFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialsHelper] Error writing credentials: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Reads credentials from an account-specific file
        /// </summary>
        public static (string email, string password)? ReadCredentialsFromFile(string accountId)
        {
            try
            {
                string workspaceDir = Path.Combine(Directory.GetCurrentDirectory(), "AccountWorkspaces", accountId);
                string credFile = Path.Combine(workspaceDir, "credentials.txt");

                if (!File.Exists(credFile))
                {
                    Console.WriteLine($"[CredentialsHelper] Credentials file not found: {credFile}");
                    return null;
                }

                string content = File.ReadAllText(credFile);
                var parts = content.Split(':');

                if (parts.Length != 2)
                {
                    Console.WriteLine($"[CredentialsHelper] Invalid credentials format in {credFile}");
                    return null;
                }

                return (parts[0], parts[1]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialsHelper] Error reading credentials: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cleans up workspace directory for an account
        /// </summary>
        public static void CleanupAccountWorkspace(string accountId)
        {
            try
            {
                string workspaceDir = Path.Combine(Directory.GetCurrentDirectory(), "AccountWorkspaces", accountId);
                
                if (Directory.Exists(workspaceDir))
                {
                    Directory.Delete(workspaceDir, recursive: true);
                    Console.WriteLine($"[CredentialsHelper] Cleaned up workspace for {accountId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialsHelper] Error cleaning workspace: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets encrypted credentials for storage (using Base64 for now)
        /// </summary>
        public static string GetEncryptedCredentials(string email, string password)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes($"{email}:{password}");
            return Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Gets display name from a RuneLite credentials.properties file
        /// </summary>
        public static string? GetDisplayName(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            
            foreach (var line in File.ReadLines(filePath))
            {
                if (line.StartsWith("JX_DISPLAY_NAME="))
                {
                    return line.Substring("JX_DISPLAY_NAME=".Length).Trim();
                }
            }
            return null;
        }

        /// <summary>
        /// Gets display name or falls back to filename
        /// </summary>
        public static string GetDisplayNameOrFallback(string filePath)
        {
            string? name = GetDisplayName(filePath);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            
            var fileName = Path.GetFileName(filePath);
            const string prefix = "credentials.properties.";
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(prefix.Length);
            
            return Path.GetFileNameWithoutExtension(fileName);
        }

        /// <summary>
        /// Validates credentials file format
        /// </summary>
        public static bool ValidateCredentialsFile(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                var content = File.ReadAllText(filePath);
                return content.Contains("JX_CHARACTER_ID=") && 
                       content.Contains("JX_SESSION_ID=");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Copies RuneLite credentials to account workspace
        /// </summary>
        public static bool CopyCredentialsToWorkspace(string accountId, string sourceCredFile)
        {
            try
            {
                if (!File.Exists(sourceCredFile))
                {
                    Console.WriteLine($"[CredentialsHelper] Source credentials not found: {sourceCredFile}");
                    return false;
                }

                string workspaceDir = Path.Combine(Directory.GetCurrentDirectory(), "AccountWorkspaces", accountId);
                Directory.CreateDirectory(workspaceDir);

                string destFile = Path.Combine(workspaceDir, "credentials.properties");
                File.Copy(sourceCredFile, destFile, overwrite: true);

                Console.WriteLine($"[CredentialsHelper] Copied credentials to workspace for {accountId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialsHelper] Error copying credentials: {ex.Message}");
                return false;
            }
        }
    }
}
