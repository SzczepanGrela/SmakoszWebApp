using Smakosz.Domain.Enums;

namespace Smakosz.Application.Common.Interfaces;

public interface IForbiddenWordService
{
    Task<bool> ContainsAsync(string text, CancellationToken ct, params ForbiddenWordCategory[] categories);
}
