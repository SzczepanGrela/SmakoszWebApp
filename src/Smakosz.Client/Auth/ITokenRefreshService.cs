namespace Smakosz.Client.Auth;

public interface ITokenRefreshService
{
    Task<string?> TryRefreshAsync(CancellationToken ct = default);
}
