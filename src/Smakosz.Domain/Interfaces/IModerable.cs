using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Interfaces;

public interface IModerable
{
    ContentModerationStatus ModerationStatus { get; set; }
}
