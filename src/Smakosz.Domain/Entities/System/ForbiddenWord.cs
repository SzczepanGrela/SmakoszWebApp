using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class ForbiddenWord
{
    public int WordId { get; set; }
    public string Word { get; set; } = string.Empty;
    public ForbiddenWordCategory Category { get; set; } = ForbiddenWordCategory.Profanity;
    public bool IsRegex { get; set; }
    public int? AddedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? AddedByUser { get; set; }
}
