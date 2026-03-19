using System.Security.Cryptography;
using System.Text;

namespace IgnaviorLauncher.Services;

public static class PasswordService
{
    public static byte[]? Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return null;
        }
        var text = Encoding.UTF8.GetBytes(plaintext);
        return ProtectedData.Protect(text, null, DataProtectionScope.CurrentUser);
    }

    public static string? Decrypt(byte[] encrypted)
    {
        if (encrypted == null)
        {
            return null;
        }
        byte[] bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    public static string HashKey(string secret)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hash);
    }
}
