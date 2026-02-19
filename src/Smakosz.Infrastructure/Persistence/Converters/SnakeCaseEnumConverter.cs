using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Smakosz.Infrastructure.Persistence.Converters;

public class SnakeCaseEnumConverter<T> : ValueConverter<T, string> where T : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(
            v => ToSnakeCase(v.ToString()),
            v => (T)Enum.Parse(typeof(T), FromSnakeCase(v), true))
    {
    }

    private static string ToSnakeCase(string value)
    {
        return Regex.Replace(value, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }

    private static string FromSnakeCase(string value)
    {
        var parts = value.Split('_');
        return string.Concat(parts.Select(p =>
            string.IsNullOrEmpty(p) ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
