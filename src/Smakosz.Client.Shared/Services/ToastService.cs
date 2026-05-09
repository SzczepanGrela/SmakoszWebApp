namespace Smakosz.Client.Services;

public class ToastService
{
    private readonly IConcurrencyConflictService? _concurrencyConflict;

    public ToastService(IConcurrencyConflictService? concurrencyConflict = null)
    {
        _concurrencyConflict = concurrencyConflict;
    }

    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, string? title = null) => Show(new ToastMessage("success", message, title));
    public void ShowError(string message, string? title = null)
    {
        if (_concurrencyConflict is { IsOpen: true })
            return;
        Show(new ToastMessage("danger", message, title));
    }
    public void ShowWarning(string message, string? title = null) => Show(new ToastMessage("warning", message, title));
    public void ShowInfo(string message, string? title = null) => Show(new ToastMessage("info", message, title));

    private void Show(ToastMessage message) => OnShow?.Invoke(message);
}

public record ToastMessage(string Type, string Message, string? Title = null);
