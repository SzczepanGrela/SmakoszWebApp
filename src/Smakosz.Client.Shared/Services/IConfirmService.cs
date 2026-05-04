namespace Smakosz.Client.Services;

public interface IConfirmService
{
    event Action? StateChanged;
    bool IsOpen { get; }
    string Message { get; }
    Task<bool> AskAsync(string message);
    void Confirm();
    void Cancel();
}
