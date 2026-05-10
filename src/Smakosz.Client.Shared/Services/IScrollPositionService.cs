namespace Smakosz.Client.Services;

public interface IScrollPositionService
{
    Task SaveCurrentPositionAsync(string uri);
    Task<bool> TryRestorePositionAsync(string uri);
    Task ScrollToElementAsync(string elementId);
}
