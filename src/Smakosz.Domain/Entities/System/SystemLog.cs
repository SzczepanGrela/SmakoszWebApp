using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class SystemLog
{
    public long Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Context { get; set; }
    public DateTime CreatedAt { get; set; }
}
