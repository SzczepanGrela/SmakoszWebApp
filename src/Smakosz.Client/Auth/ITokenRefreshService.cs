namespace Smakosz.Client.Auth;

public interface ITokenRefreshService
{
    Task<bool> TryRefreshAsync(CancellationToken ct = default);
}
