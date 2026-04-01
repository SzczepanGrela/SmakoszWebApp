using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int MemorySize = 19456; // 19 MiB
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    public string Hash(string input)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = ComputeHash(input, salt);

        var saltB64 = ToBase64NoPadding(salt);
        var hashB64 = ToBase64NoPadding(hash);

        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={Parallelism}${saltB64}${hashB64}";
    }

    public bool Verify(string input, string hash)
    {
        if (!TryParsePhc(hash, out var salt, out var expectedHash))
            return false;

        var computedHash = ComputeHash(input, salt);
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static byte[] ComputeHash(string input, byte[] salt)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(input))
        {
            Salt = salt,
            MemorySize = MemorySize,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };
        return argon2.GetBytes(HashLength);
    }

    private static bool TryParsePhc(string phc, out byte[] salt, out byte[] hash)
    {
        salt = [];
        hash = [];

        var parts = phc.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id")
            return false;

        try
        {
            salt = FromBase64NoPadding(parts[4]);
            hash = FromBase64NoPadding(parts[5]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ToBase64NoPadding(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=');

    private static byte[] FromBase64NoPadding(string base64)
    {
        var remainder = base64.Length % 4;
        var padded = remainder switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64
        };
        return Convert.FromBase64String(padded);
    }
}
