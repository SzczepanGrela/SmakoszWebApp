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

    public async Task<bool> ModeratePhotoAsync(Guid id, bool approve, IReadOnlyList<string>? reasonCodes = null, string? moderatorNote = null)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/photos/{id}/moderate",
            new { Approve = approve, ReasonCodes = reasonCodes, ModeratorNote = moderatorNote });
        return response.Success;
    }

    public Task<PagedResult<AdminReviewDto>?> GetPendingReviewsAsync(int page = 1)
        => _api.GetAsync<PagedResult<AdminReviewDto>>($"/api/admin/reviews/pending?page={page}");

    public async Task<bool> ModerateReviewAsync(Guid id, bool approve, IReadOnlyList<string>? reasonCodes = null, string? moderatorNote = null)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/reviews/{id}/moderate",
            new { Approve = approve, ReasonCodes = reasonCodes, ModeratorNote = moderatorNote });
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

    public Task<PagedResult<AdminEditRequestDto>?> GetEditRequestsAsync(int page = 1, int? restaurantId = null)
    {
        var url = $"/api/admin/edit-requests?page={page}";
        if (restaurantId.HasValue) url += $"&restaurantId={restaurantId.Value}";
        return _api.GetAsync<PagedResult<AdminEditRequestDto>>(url);
    }

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

    public async Task<bool> Disable2faForUserAsync(Guid publicId)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/users/{publicId}/disable-2fa", null);
        return response.Success;
    }

    public async Task<bool> ResetUserPasswordAsync(Guid publicId)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/users/{publicId}/reset-password", null);
        return response.Success;
    }

    public Task<PagedResult<AdminRestaurantDto>?> GetRestaurantsAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminRestaurantDto>>($"/api/admin/restaurants?page={page}&search={search}");

    public Task<AdminRestaurantDetailDto?> GetRestaurantDetailAsync(int id)
        => _api.GetAsync<AdminRestaurantDetailDto>($"/api/admin/restaurants/by-id/{id}");

    public async Task<bool> UpdateRestaurantAsync(Guid publicId, AdminRestaurantUpdateDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/restaurants/{publicId}", dto);
        return response.Success;
    }

    public async Task<bool> ChangeRestaurantStatusAsync(Guid publicId, string status, string? reason)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/restaurants/{publicId}/status",
            new { status, reason });
        return response.Success;
    }

    public Task<PagedResult<AdminModerationLogDto>?> GetRestaurantModerationHistoryAsync(int restaurantId, int page = 1)
        => _api.GetAsync<PagedResult<AdminModerationLogDto>>($"/api/admin/restaurants/by-id/{restaurantId}/moderation-history?page={page}");

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

    public Task<PagedResult<AdminAuditLogDto>?> GetAuditLogsAsync(int page = 1, string? tableName = null, int? recordId = null)
    {
        var url = $"/api/admin/audit-logs?page={page}&tableName={tableName}";
        if (recordId.HasValue) url += $"&recordId={recordId.Value}";
        return _api.GetAsync<PagedResult<AdminAuditLogDto>>(url);
    }

    public Task<PagedResult<AdminSecurityLogDto>?> GetSecurityLogsAsync(int page = 1, string? eventType = null)
        => _api.GetAsync<PagedResult<AdminSecurityLogDto>>($"/api/admin/security-logs?page={page}&eventType={eventType}");

    public Task<PagedResult<AdminEmailLogDto>?> GetEmailLogsAsync(int page = 1, string? status = null, string? type = null)
        => _api.GetAsync<PagedResult<AdminEmailLogDto>>($"/api/admin/email-logs?page={page}&status={status}&type={type}");

    public Task<PagedResult<AdminModerationLogDto>?> GetModerationLogsAsync(int page = 1, string? actor = null, string? entityType = null)
        => _api.GetAsync<PagedResult<AdminModerationLogDto>>($"/api/admin/moderation-logs?page={page}&actor={actor}&entityType={entityType}");

    public Task<PagedResult<AdminAiLogDto>?> GetAiLogsAsync(int page = 1, string? modelType = null, bool? fallback = null)
    {
        var url = $"/api/admin/ai-logs?page={page}&modelType={modelType}";
        if (fallback.HasValue) url += $"&fallback={fallback.Value.ToString().ToLower()}";
        return _api.GetAsync<PagedResult<AdminAiLogDto>>(url);
    }

    public async Task<List<AdminSystemNodeDto>?> GetSystemNodesAsync()
        => await _api.GetAsync<List<AdminSystemNodeDto>>("/api/admin/nodes");

    public Task<PagedResult<AdminBannedIdentifierDto>?> GetBannedIdentifiersAsync(int page = 1, string? type = null, bool includeExpired = false)
    {
        var url = $"/api/admin/banned-identifiers?page={page}&includeExpired={includeExpired}";
        if (!string.IsNullOrEmpty(type)) url += $"&type={type}";
        return _api.GetAsync<PagedResult<AdminBannedIdentifierDto>>(url);
    }

    public async Task<bool> CreateBannedIdentifierAsync(AdminBannedIdentifierDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/banned-identifiers",
            new { dto.Type, dto.Value, dto.Reason, dto.ExpiresAt });
        return response.Success;
    }

    public async Task<bool> UpdateBannedIdentifierAsync(int id, object dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/banned-identifiers/{id}", dto);
        return response.Success;
    }

    public async Task<bool> DeleteBannedIdentifierAsync(int id)
        => await _api.DeleteAsync($"/api/admin/banned-identifiers/{id}");

    public Task<PagedResult<AdminForbiddenWordDto>?> GetForbiddenWordsAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminForbiddenWordDto>>($"/api/admin/forbidden-words?page={page}&search={search}");

    public async Task<bool> CreateForbiddenWordAsync(AdminForbiddenWordDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/forbidden-words",
            new { dto.Word, dto.Category, dto.IsRegex });
        return response.Success;
    }

    public async Task<bool> UpdateForbiddenWordAsync(int id, AdminForbiddenWordDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/forbidden-words/{id}",
            new { dto.Word, dto.Category, dto.IsRegex });
        return response.Success;
    }

    public async Task<bool> DeleteForbiddenWordAsync(int id)
        => await _api.DeleteAsync($"/api/admin/forbidden-words/{id}");

    public async Task<bool?> TestForbiddenWordAsync(string text, string[] categories)
    {
        var response = await _api.PostApiResponseAsync<bool>("/api/admin/forbidden-words/test",
            new { text, categories });
        if (!response.Success) return null;
        return response.Data;
    }

    public Task<PagedResult<AdminRejectionReasonDto>?> GetRejectionReasonsAsync(int page = 1, int pageSize = 100, string? category = null, bool includeInactive = false)
    {
        var url = $"/api/admin/rejection-reasons?page={page}&pageSize={pageSize}&includeInactive={includeInactive}";
        if (!string.IsNullOrEmpty(category)) url += $"&category={category}";
        return _api.GetAsync<PagedResult<AdminRejectionReasonDto>>(url);
    }

    public async Task<bool> CreateRejectionReasonAsync(AdminRejectionReasonDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/rejection-reasons",
            new { dto.ReasonCode, dto.Category, dto.AdminLabel, dto.UserMessageTemplate, dto.IsActive });
        return response.Success;
    }

    public async Task<bool> UpdateRejectionReasonAsync(string reasonCode, AdminRejectionReasonDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/rejection-reasons/{reasonCode}",
            new { dto.Category, dto.AdminLabel, dto.UserMessageTemplate, dto.IsActive });
        return response.Success;
    }

    public async Task<bool> DeleteRejectionReasonAsync(string reasonCode)
        => await _api.DeleteAsync($"/api/admin/rejection-reasons/{reasonCode}");

    public Task<PagedResult<AdminDishDto>?> GetAdminDishesAsync(int page = 1, string? search = null, int? restaurantId = null, string? moderationStatus = null, bool? isAvailable = null)
    {
        var url = $"/api/admin/dishes?page={page}&search={search}&moderationStatus={moderationStatus}";
        if (restaurantId.HasValue) url += $"&restaurantId={restaurantId.Value}";
        if (isAvailable.HasValue) url += $"&isAvailable={isAvailable.Value.ToString().ToLower()}";
        return _api.GetAsync<PagedResult<AdminDishDto>>(url);
    }

    public async Task<bool> DeleteAdminDishAsync(Guid publicId)
        => await _api.DeleteAsync($"/api/admin/dishes/{publicId}");

    public async Task<bool> ChangeAdminDishModerationStatusAsync(Guid publicId, string status)
    {
        var response = await _api.PutApiResponseAsync<object>(
            $"/api/admin/dishes/{publicId}/moderation-status", new { Status = status });
        return response.Success;
    }

    public async Task<bool> SetAdminDishAvailabilityAsync(Guid publicId, bool isAvailable)
    {
        var response = await _api.PutApiResponseAsync<object>(
            $"/api/admin/dishes/{publicId}/availability", new { IsAvailable = isAvailable });
        return response.Success;
    }
}
