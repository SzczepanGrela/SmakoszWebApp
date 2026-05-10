using Microsoft.JSInterop;

namespace Smakosz.Client.Services;

public class ScrollPositionService : IScrollPositionService
{
    private readonly IJSRuntime _js;
    private readonly Dictionary<string, double> _positions = new();

    public ScrollPositionService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task SaveCurrentPositionAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return;

        var y = await _js.InvokeAsync<double>("smakoszScroll.getY");
        _positions[uri] = y;
    }

    public async Task<bool> TryRestorePositionAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri) || !_positions.TryGetValue(uri, out var y))
            return false;

        await _js.InvokeVoidAsync("smakoszScroll.setY", y);
        return true;
    }

    public async Task ScrollToElementAsync(string elementId)
    {
        if (string.IsNullOrEmpty(elementId))
            return;

        await _js.InvokeVoidAsync("smakoszScroll.scrollToElement", elementId);
    }
}
