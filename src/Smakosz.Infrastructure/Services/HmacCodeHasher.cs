using System.Security.Cryptography;
using System.Text;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class HmacCodeHasher : ICodeHasher
{
    private readonly byte[] _key;

    public HmacCodeHasher(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _key = Encoding.UTF8.GetBytes(key);
    }

    public string Hash(string input)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    public bool Verify(string input, string hash)
    {
        var computed = Hash(input);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(hash));
    }
}
