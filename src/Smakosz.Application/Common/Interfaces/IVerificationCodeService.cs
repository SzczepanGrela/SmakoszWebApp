using Smakosz.Domain.Enums;

namespace Smakosz.Application.Common.Interfaces;

public interface IVerificationCodeService
{
    Task<string> CreateCodeAsync(int userId, VerificationCodeType type, CancellationToken ct);
}
