using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
