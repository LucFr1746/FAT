using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FAT.App.Helpers;

/// <summary>
/// Secure local storage helper for "Remember Me" credentials using Windows DPAPI.
/// Data is encrypted per user profile and saved locally.
/// </summary>
public static class CredentialStorage
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FAT",
        "saved_credentials.dat"
    );

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FAT_FPT_SECURE_ENTROPY_2026");

    public sealed record SavedCredentials(string Username, string Password, bool IsGoogleLogin);

    public static void SaveCredentials(string username, string password, bool isGoogleLogin = false)
    {
        try
        {
            var data = new SavedCredentials(username, password, isGoogleLogin);
            var json = JsonSerializer.Serialize(data);
            var plainBytes = Encoding.UTF8.GetBytes(json);

            var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);

            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(StoragePath, encryptedBytes);
        }
        catch
        {
            // Ignore encryption or I/O failures gracefully
        }
    }

    public static SavedCredentials? LoadCredentials()
    {
        try
        {
            if (!File.Exists(StoragePath))
            {
                return null;
            }

            var encryptedBytes = File.ReadAllBytes(StoragePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);

            return JsonSerializer.Deserialize<SavedCredentials>(json);
        }
        catch
        {
            ClearCredentials();
            return null;
        }
    }

    public static void ClearCredentials()
    {
        try
        {
            if (File.Exists(StoragePath))
            {
                File.Delete(StoragePath);
            }
        }
        catch
        {
            // Ignore deletion failure
        }
    }
}
