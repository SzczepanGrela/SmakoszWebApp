namespace Smakosz.Client.Services;

public class ConfirmService : IConfirmService
{
    private TaskCompletionSource<bool>? _tcs;

    public event Action? StateChanged;

    public bool IsOpen => _tcs is { Task.IsCompleted: false };
    public string Message { get; private set; } = "";

    public Task<bool> AskAsync(string message)
    {
        // A pending dialog gets dismissed as cancelled before opening a new one.
        _tcs?.TrySetResult(false);

        Message = message;
        _tcs = new TaskCompletionSource<bool>();
        StateChanged?.Invoke();
        return _tcs.Task;
    }

    public void Confirm()
    {
        var tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(true);
        StateChanged?.Invoke();
    }

    public void Cancel()
    {
        var tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(false);
        StateChanged?.Invoke();
    }
}
