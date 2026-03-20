using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Smakosz.Infrastructure.Persistence.Converters;

public class UpperCaseEnumConverter<T> : ValueConverter<T, string> where T : struct, Enum
{
    public UpperCaseEnumConverter()
        : base(
            v => v.ToString().ToUpperInvariant(),
            v => (T)Enum.Parse(typeof(T), v, true))
    {
    }
}
