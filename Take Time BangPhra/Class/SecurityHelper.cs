using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Security helper class for password hashing and validation
/// </summary>
public static class SecurityHelper
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 10000;

    /// <summary>
    /// Hash a password using PBKDF2 with salt
    /// Returns format: {iterations}.{salt}.{hash}
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException("password");

        // Generate salt
        byte[] salt;
        using (var rng = new RNGCryptoServiceProvider())
        {
            salt = new byte[SaltSize];
            rng.GetBytes(salt);
        }

        // Generate hash
        byte[] hash;
        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
        {
            hash = pbkdf2.GetBytes(HashSize);
        }

        // Return formatted string
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verify a password against a hash
    /// </summary>
    public static bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        // Check if this is an old-style plain text password (legacy support)
        if (!hashedPassword.Contains("."))
        {
            // Legacy plain text comparison (case-sensitive)
            return password == hashedPassword;
        }

        try
        {
            // Parse the hash
            var parts = hashedPassword.Split('.');
            if (parts.Length != 3)
                return false;

            int iterations = int.Parse(parts[0]);
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] storedHash = Convert.FromBase64String(parts[2]);

            // Compute hash with same parameters
            byte[] computedHash;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                computedHash = pbkdf2.GetBytes(storedHash.Length);
            }

            // Constant-time comparison to prevent timing attacks
            return SlowEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Constant-time comparison to prevent timing attacks
    /// </summary>
    private static bool SlowEquals(byte[] a, byte[] b)
    {
        uint diff = (uint)a.Length ^ (uint)b.Length;
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            diff |= (uint)(a[i] ^ b[i]);
        }
        return diff == 0;
    }

    /// <summary>
    /// Check if a password hash needs to be upgraded (is plain text)
    /// </summary>
    public static bool NeedsUpgrade(string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
            return false;

        // If it doesn't contain dots, it's plain text and needs upgrade
        return !hashedPassword.Contains(".");
    }

    /// <summary>
    /// Generate a random password
    /// </summary>
    public static string GenerateRandomPassword(int length = 12)
    {
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%";
        var sb = new StringBuilder();
        using (var rng = new RNGCryptoServiceProvider())
        {
            byte[] bytes = new byte[length];
            rng.GetBytes(bytes);
            for (int i = 0; i < length; i++)
            {
                sb.Append(validChars[bytes[i] % validChars.Length]);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Validate password strength
    /// </summary>
    public static (bool IsValid, string Message) ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (false, "กรุณากรอกรหัสผ่าน");

        if (password.Length < 6)
            return (false, "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร");

        // Basic validation - can be enhanced
        return (true, "");
    }
}
