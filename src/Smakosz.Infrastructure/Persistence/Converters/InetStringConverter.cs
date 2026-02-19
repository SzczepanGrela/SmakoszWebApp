using System.Net;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Smakosz.Infrastructure.Persistence.Converters;

public class InetStringConverter : ValueConverter<string, IPAddress>
{
    public InetStringConverter()
        : base(
            v => IPAddress.Parse(v),
            v => v.ToString())
    {
    }
}
