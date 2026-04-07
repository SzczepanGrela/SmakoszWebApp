using Smakosz.Application.Common.Interfaces;

namespace Smakosz.UnitTests.Common.TestInfrastructure;

public class StubValidationConfigProvider : IValidationConfigProvider
{
    private readonly Dictionary<string, string> _values;

    public StubValidationConfigProvider(Dictionary<string, string>? values = null)
    {
        _values = values ?? new Dictionary<string, string>();
    }

    public int GetInt(string key, int defaultValue)
    {
        return _values.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue)
    {
        return _values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : defaultValue;
    }
}
