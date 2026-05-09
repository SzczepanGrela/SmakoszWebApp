using Microsoft.AspNetCore.Components;

namespace Smakosz.Client.Services;

public class ConcurrencyConflictService : IConcurrencyConflictService
{
    private readonly NavigationManager _navigation;

    public ConcurrencyConflictService(NavigationManager navigation)
    {
        _navigation = navigation;
    }

    public event Action? StateChanged;

    public bool IsOpen { get; private set; }

    public void Show()
    {
        if (IsOpen) return;
        IsOpen = true;
        StateChanged?.Invoke();
    }

    public void Dismiss()
    {
        if (!IsOpen) return;
        IsOpen = false;
        StateChanged?.Invoke();
    }

    public void Refresh()
    {
        IsOpen = false;
        StateChanged?.Invoke();
        _navigation.NavigateTo(_navigation.Uri, forceLoad: true);
    }
}
