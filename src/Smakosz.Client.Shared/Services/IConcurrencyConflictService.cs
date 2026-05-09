namespace Smakosz.Client.Services;

public interface IConcurrencyConflictService
{
    event Action? StateChanged;
    bool IsOpen { get; }
    void Show();
    void Dismiss();
    void Refresh();
}
