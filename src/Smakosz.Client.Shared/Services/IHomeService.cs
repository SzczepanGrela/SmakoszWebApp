using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IHomeService
{
    Task<HomeDataDto?> GetHomeDataAsync();
}
