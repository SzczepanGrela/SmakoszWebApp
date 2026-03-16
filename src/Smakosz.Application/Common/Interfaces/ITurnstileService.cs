namespace Smakosz.Application.Common.Interfaces;

public interface ITurnstileService
{
    Task<bool> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
