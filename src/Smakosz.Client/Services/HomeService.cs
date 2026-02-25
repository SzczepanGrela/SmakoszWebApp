using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class HomeService : IHomeService
{
    private readonly SmakoszApiClient _api;

    public HomeService(SmakoszApiClient api) => _api = api;

    public Task<HomeDataDto?> GetHomeDataAsync()
        => _api.GetAsync<HomeDataDto>("/api/home");
}
