using Microsoft.Extensions.Logging;

namespace Smakosz.Infrastructure.Logging;

public class DbLoggerOptions
{
    public LogLevel MinLevel { get; set; } = LogLevel.Warning;
    public HashSet<string> IgnoredPrefixes { get; set; } = ["Microsoft", "System", "Hangfire"];
    public int BatchSize { get; set; } = 10;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);
}
