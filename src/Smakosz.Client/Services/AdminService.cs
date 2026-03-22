using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class AdminService : IAdminService
{
    private readonly SmakoszApiClient _api;

    public AdminService(SmakoszApiClient api) => _api = api;

    public Task<AdminDashboardDto?> GetDashboardAsync()
        => _api.GetAsync<AdminDashboardDto>("/api/admin/dashboard");

    public Task<PagedResult<AdminTicketDto>?> GetTicketsAsync(int page = 1, string? status = null, string? ticketType = null)
        => _api.GetAsync<PagedResult<AdminTicketDto>>($"/api/admin/tickets?page={page}&status={status}&ticketType={ticketType}");

    public Task<AdminTicketDetailDto?> GetTicketDetailAsync(int id)
        => _api.GetAsync<AdminTicketDetailDto>($"/api/admin/tickets/{id}");

    public async Task<bool> UpdateTicketStatusAsync(int id, string status)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/tickets/{id}/status", new { Status = status });
        return response.Success;
    }

    public async Task<bool> RespondToContactAsync(int id, string response)
    {
        var result = await _api.PostApiResponseAsync<object>($"/api/admin/tickets/{id}/respond",
            new { Response = response });
        return result.Success;
    }

    public Task<PagedResult<AdminPhotoDto>?> GetPendingPhotosAsync(int page = 1)
        => _api.GetAsync<PagedResult<AdminPhotoDto>>($"/api/admin/photos/pending?page={page}");

    public async Task<bool> ModeratePhotoAsync(Guid id, string action, string? reason = null)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/photos/{id}/moderate",
            new { Approve = action == "approve", RejectionReason = reason });
        return response.Success;
    }

    public Task<PagedResult<AdminReviewDto>?> GetPendingReviewsAsync(int page = 1)
        => _api.GetAsync<PagedResult<AdminReviewDto>>($"/api/admin/reviews/pending?page={page}");

    public async Task<bool> ModerateReviewAsync(Guid id, string action, string? reason = null)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/reviews/{id}/moderate",
            new { Approve = action == "approve", RejectionReason = reason });
        return response.Success;
    }

    public Task<PagedResult<AdminReportDto>?> GetReportsAsync(int page = 1, string? status = null)
        => _api.GetAsync<PagedResult<AdminReportDto>>($"/api/admin/reports?page={page}&status={status}");

    public async Task<bool> ResolveReportAsync(int id, string resolution)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/reports/{id}/status",
            new { Status = resolution });
        return response.Success;
    }

    public Task<PagedResult<AdminEditRequestDto>?> GetEditRequestsAsync(int page = 1)
        => _api.GetAsync<PagedResult<AdminEditRequestDto>>($"/api/admin/edit-requests?page={page}");

    public async Task<bool> ProcessEditRequestAsync(int id, string action, string? reason = null)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/edit-requests/{id}/process",
            new { Approve = action == "approve", RejectionReason = reason });
        return response.Success;
    }

    public Task<PagedResult<AdminUserDto>?> GetUsersAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminUserDto>>($"/api/admin/users?page={page}&search={search}");

    public Task<AdminUserDto?> GetUserAsync(int userId)
        => _api.GetAsync<AdminUserDto>($"/api/admin/users/{userId}");

    public async Task<bool> UpdateUserAsync(Guid publicId, string action)
    {
        var response = action switch
        {
            "ban" => await _api.PostApiResponseAsync<object>($"/api/admin/users/{publicId}/ban", null),
            "unban" => await _api.PostApiResponseAsync<object>($"/api/admin/users/{publicId}/unban", null),
            _ => new ApiResponse<object> { Success = false }
        };
        return response.Success;
    }

    public Task<PagedResult<AdminRestaurantDto>?> GetRestaurantsAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminRestaurantDto>>($"/api/admin/restaurants?page={page}&search={search}");

    public async Task<bool> VerifyRestaurantAsync(Guid publicId)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/restaurants/{publicId}/verify", null);
        return response.Success;
    }

    public Task<PagedResult<AdminIngredientDto>?> GetIngredientsAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminIngredientDto>>($"/api/admin/ingredients?page={page}&search={search}");

    public async Task<bool> CreateIngredientAsync(AdminIngredientDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/ingredients", dto);
        return response.Success;
    }

    public async Task<bool> UpdateIngredientAsync(int id, AdminIngredientDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/ingredients/{id}", dto);
        return response.Success;
    }

    public async Task<bool> DeleteIngredientAsync(int id)
        => await _api.DeleteAsync($"/api/admin/ingredients/{id}");

    public Task<PagedResult<AdminCityDto>?> GetCitiesAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminCityDto>>($"/api/admin/cities?page={page}&search={search}");

    public async Task<bool> CreateCityAsync(AdminCityDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/cities", dto);
        return response.Success;
    }

    public async Task<bool> UpdateCityAsync(int id, AdminCityDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/cities/{id}", dto);
        return response.Success;
    }

    public async Task<bool> DeleteCityAsync(int id)
        => await _api.DeleteAsync($"/api/admin/cities/{id}");

    public Task<PagedResult<AdminTagDto>?> GetTagsAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminTagDto>>($"/api/admin/tags?page={page}&search={search}");

    public async Task<bool> CreateTagAsync(AdminTagDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/tags", dto);
        return response.Success;
    }

    public async Task<bool> UpdateTagAsync(int id, AdminTagDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/tags/{id}", dto);
        return response.Success;
    }

    public async Task<bool> DeleteTagAsync(int id)
        => await _api.DeleteAsync($"/api/admin/tags/{id}");

    public async Task<List<AdminSystemConfigDto>> GetSystemConfigAsync()
        => await _api.GetAsync<List<AdminSystemConfigDto>>("/api/admin/system-config") ?? [];

    public async Task<bool> UpdateSystemConfigAsync(string key, string value)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/admin/system-config",
            new { Key = key, Value = value });
        return response.Success;
    }

    public Task<PagedResult<AdminLogEntryDto>?> GetLogsAsync(int page = 1, string? level = null)
        => _api.GetAsync<PagedResult<AdminLogEntryDto>>($"/api/admin/logs?page={page}&level={level}");

    public async Task<List<AdminAiModelDto>> GetAiModelsAsync()
        => await _api.GetAsync<List<AdminAiModelDto>>("/api/admin/ai-models") ?? [];

    public Task<PagedResult<AdminJobDto>?> GetJobsAsync(int page = 1)
        => _api.GetAsync<PagedResult<AdminJobDto>>($"/api/admin/jobs?page={page}");

    public async Task<bool> TriggerJobAsync(int id)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/jobs/{id}/trigger", null);
        return response.Success;
    }

    public async Task<bool> CreateJobAsync(CreateJobRequest request)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/jobs", request);
        return response.Success;
    }

    public async Task<bool> CancelJobAsync(int id)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/jobs/{id}/cancel", null);
        return response.Success;
    }

    public async Task<(bool Success, string? ErrorMessage)> ScheduleNcfTrainingAsync()
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/ncf-training/schedule", null);
        return (response.Success, response.Error?.Message);
    }

    public Task<PagedResult<AdminIngredientSuggestionDto>?> GetIngredientSuggestionsAsync(int page = 1, string? status = null)
        => _api.GetAsync<PagedResult<AdminIngredientSuggestionDto>>($"/api/admin/ingredient-suggestions?page={page}&status={status}");

    public async Task<bool> ReviewIngredientSuggestionAsync(int id, bool approve, string? adminNote = null,
        bool? isAllergen = null, bool? isVegetarian = null, bool? isVegan = null,
        bool? isGlutenFree = null, bool? isLactoseFree = null, string? iconUrl = null)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/ingredient-suggestions/{id}/review",
            new { Approve = approve, AdminNote = adminNote,
                IsAllergen = isAllergen, IsVegetarian = isVegetarian, IsVegan = isVegan,
                IsGlutenFree = isGlutenFree, IsLactoseFree = isLactoseFree, IconUrl = iconUrl });
        return response.Success;
    }

    public async Task<List<AdminHeroImageDto>> GetHeroImagesAsync()
        => await _api.GetAsync<List<AdminHeroImageDto>>("/api/admin/hero-images") ?? [];

    public async Task<bool> DeleteHeroImageAsync(Guid publicId)
        => await _api.DeleteAsync($"/api/media/{publicId}");

    public Task<PagedResult<AdminAuditLogDto>?> GetAuditLogsAsync(int page = 1, string? tableName = null)
        => _api.GetAsync<PagedResult<AdminAuditLogDto>>($"/api/admin/audit-logs?page={page}&tableName={tableName}");

    public Task<PagedResult<AdminSecurityLogDto>?> GetSecurityLogsAsync(int page = 1, string? eventType = null)
        => _api.GetAsync<PagedResult<AdminSecurityLogDto>>($"/api/admin/security-logs?page={page}&eventType={eventType}");

    public async Task<List<AdminSystemNodeDto>?> GetSystemNodesAsync()
        => await _api.GetAsync<List<AdminSystemNodeDto>>("/api/admin/nodes");
}
