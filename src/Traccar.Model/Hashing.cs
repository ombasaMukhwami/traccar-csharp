using System.Security.Cryptography;

namespace Traccar.Model;

public static class Hashing
{
    private const int Iterations = 1000;
    private const int SaltSize = 24;
    private const int HashSize = 24;

    public readonly record struct HashingResult(string Hash, string Salt);

    public static HashingResult CreateHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt);
        return new HashingResult(Convert.ToHexStringLower(hash), Convert.ToHexStringLower(salt));
    }

    public static bool ValidatePassword(string password, string? hashHex, string? saltHex)
    {
        if (hashHex is null || saltHex is null)
        {
            return false;
        }
        var hash = Convert.FromHexString(hashHex);
        var salt = Convert.FromHexString(saltHex);
        return CryptographicOperations.FixedTimeEquals(hash, Derive(password, salt));
    }

    private static byte[] Derive(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA1, HashSize);
    }
}
