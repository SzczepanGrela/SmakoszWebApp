namespace Smakosz.Application.Common.Interfaces;

public interface IValidationConfigProvider
{
    int GetInt(string key, int defaultValue);
    bool GetBool(string key, bool defaultValue);
}
