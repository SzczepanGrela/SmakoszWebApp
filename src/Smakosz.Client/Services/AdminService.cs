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

    public Task<List<TicketSummaryDto>?> GetTicketsSummaryAsync()
        => _api.GetAsync<List<TicketSummaryDto>>("/api/admin/tickets/summary");

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

    public async Task<BulkModeratePhotosResultDto?> BulkModeratePhotosAsync(IReadOnlyList<Guid> publicIds, bool approve, IReadOnlyList<string>? reasonCodes = null, string? moderatorNote = null)
    {
        var response = await _api.PostApiResponseAsync<BulkModeratePhotosResultDto>("/api/admin/photos/bulk-moderate",
            new { PublicIds = publicIds, Approve = approve, ReasonCodes = reasonCodes, ModeratorNote = moderatorNote });
        return response.Success ? response.Data : null;
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

    public Task<PagedResult<AdminUserDto>?> GetUsersAsync(int page = 1, string? search = null, string? role = null)
    {
        var query = $"/api/admin/users?page={page}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(role)) query += $"&role={role}";
        return _api.GetAsync<PagedResult<AdminUserDto>>(query);
    }

    public Task<AdminUserDetailDto?> GetUserAsync(Guid publicId)
        => _api.GetAsync<AdminUserDetailDto>($"/api/admin/users/{publicId}");

    public Task<PagedResult<AdminUserReviewDto>?> GetUserReviewsAsync(Guid publicId, int page = 1)
        => _api.GetAsync<PagedResult<AdminUserReviewDto>>($"/api/admin/users/{publicId}/reviews?page={page}");

    public Task<PagedResult<AdminSecurityLogDto>?> GetUserSecurityLogsAsync(Guid publicId, int page = 1)
        => _api.GetAsync<PagedResult<AdminSecurityLogDto>>($"/api/admin/users/{publicId}/security-logs?page={page}");

    public Task<PagedResult<AdminPhotoDto>?> GetUserPhotosAsync(Guid publicId, int page = 1)
        => _api.GetAsync<PagedResult<AdminPhotoDto>>($"/api/admin/users/{publicId}/photos?page={page}");

    public Task<PagedResult<AdminTicketDto>?> GetUserTicketsAsync(Guid publicId, int page = 1)
        => _api.GetAsync<PagedResult<AdminTicketDto>>($"/api/admin/users/{publicId}/tickets?page={page}");

    public Task<PagedResult<AdminUserActionLogDto>?> GetUserActionLogsAsync(Guid publicId, int page = 1)
        => _api.GetAsync<PagedResult<AdminUserActionLogDto>>($"/api/admin/users/{publicId}/action-logs?page={page}");

    public Task<PagedResult<AdminUserFollowerDto>?> GetUserFollowersAsync(Guid publicId, int page = 1)
        => _api.GetAsync<PagedResult<AdminUserFollowerDto>>($"/api/admin/users/{publicId}/followers?page={page}");

    public Task<PagedResult<AdminUserRestaurantClaimDto>?> GetUserRestaurantClaimsAsync(Guid publicId, int page = 1)
        => _api.GetAsync<PagedResult<AdminUserRestaurantClaimDto>>($"/api/admin/users/{publicId}/restaurant-claims?page={page}");

    public async Task<(bool Success, string? ErrorMessage)> ChangeUserEmailAsync(Guid publicId, string newEmail)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/users/{publicId}/email", new { Email = newEmail });
        return (response.Success, response.Error?.Message);
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangeUserUsernameAsync(Guid publicId, string newUsername)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/users/{publicId}/username", new { Username = newUsername });
        return (response.Success, response.Error?.Message);
    }

    public async Task<bool> DeactivateUserAsync(Guid publicId)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/users/{publicId}/deactivate", null);
        return response.Success;
    }

    public async Task<bool> ActivateUserAsync(Guid publicId)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/users/{publicId}/activate", null);
        return response.Success;
    }

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

    public async Task<Guid?> CreatePrivilegedAccountAsync(string email, string username, string role)
    {
        var response = await _api.PostApiResponseAsync<Guid>("/api/admin/users", new { Email = email, Username = username, Role = role });
        return response.Success ? response.Data : null;
    }

    public async Task<bool> ChangeUserRoleAsync(Guid publicId, string newRole, string? reason)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/users/{publicId}/role", new { Role = newRole, Reason = reason });
        return response.Success;
    }

    public Task<PagedResult<AdminRestaurantDto>?> GetRestaurantsAsync(int page = 1, string? search = null, bool? isOrphan = null)
    {
        var url = $"/api/admin/restaurants?page={page}&search={search}";
        if (isOrphan.HasValue) url += $"&isOrphan={isOrphan.Value.ToString().ToLower()}";
        return _api.GetAsync<PagedResult<AdminRestaurantDto>>(url);
    }

    public async Task<int?> CreateRestaurantAsync(AdminCreateRestaurantDto dto)
    {
        var response = await _api.PostApiResponseAsync<int>("/api/admin/restaurants", dto);
        return response.Success ? response.Data : null;
    }

    public async Task<bool> ApproveRestaurantClaimAsync(int ticketId)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/tickets/{ticketId}/approve-claim", null);
        return response.Success;
    }

    public async Task<bool> RejectRestaurantClaimAsync(int ticketId, string reason)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/tickets/{ticketId}/reject-claim", new { Reason = reason });
        return response.Success;
    }

    public async Task<bool> RejectNewRestaurantRequestAsync(int ticketId, string reason)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/tickets/{ticketId}/reject-request", new { Reason = reason });
        return response.Success;
    }

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
        // Backend expects { Name, IsAllergen, IsVegetarian, IsVegan, IsGlutenFree, IsLactoseFree }; DTO uses IngredientName.
        var response = await _api.PostApiResponseAsync<object>("/api/admin/ingredients",
            new { Name = dto.IngredientName, dto.IsAllergen, dto.IsVegetarian, dto.IsVegan, IsGlutenFree = false, IsLactoseFree = false });
        return response.Success;
    }

    public async Task<bool> UpdateIngredientAsync(int id, AdminIngredientDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/ingredients/{id}",
            new { Name = dto.IngredientName, dto.IsAllergen, dto.IsVegetarian, dto.IsVegan, IsGlutenFree = (bool?)null, IsLactoseFree = (bool?)null });
        return response.Success;
    }

    public async Task<bool> DeleteIngredientAsync(int id)
        => await _api.DeleteAsync($"/api/admin/ingredients/{id}");

    public Task<PagedResult<AdminCityDto>?> GetCitiesAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminCityDto>>($"/api/admin/cities?page={page}&search={search}");

    public async Task<bool> CreateCityAsync(AdminCityDto dto)
    {
        // Backend expects { Name, Region }; AdminCityDto exposes CityName for display, so reshape here.
        var response = await _api.PostApiResponseAsync<object>("/api/admin/cities", new { Name = dto.CityName, Region = dto.Region });
        return response.Success;
    }

    public async Task<bool> UpdateCityAsync(int id, AdminCityDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/cities/{id}", new { Name = dto.CityName, Region = dto.Region });
        return response.Success;
    }

    public async Task<bool> DeleteCityAsync(int id)
        => await _api.DeleteAsync($"/api/admin/cities/{id}");

    public Task<PagedResult<AdminCuisineTypeDto>?> GetCuisineTypesAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminCuisineTypeDto>>($"/api/admin/cuisine-types?page={page}&search={search}");

    public async Task<bool> CreateCuisineTypeAsync(AdminCuisineTypeDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/cuisine-types", dto);
        return response.Success;
    }

    public async Task<bool> UpdateCuisineTypeAsync(int id, AdminCuisineTypeDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/cuisine-types/{id}", dto);
        return response.Success;
    }

    public async Task<bool> DeleteCuisineTypeAsync(int id)
        => await _api.DeleteAsync($"/api/admin/cuisine-types/{id}");

    public Task<PagedResult<AdminTagDto>?> GetTagsAsync(int page = 1, string? search = null)
        => _api.GetAsync<PagedResult<AdminTagDto>>($"/api/admin/tags?page={page}&search={search}");

    public async Task<bool> CreateTagAsync(AdminTagDto dto)
    {
        // Backend expects { Name, Category, TargetEntity, DisplayColor }; DTO uses TagName.
        var response = await _api.PostApiResponseAsync<object>("/api/admin/tags",
            new { Name = dto.TagName, dto.Category, dto.TargetEntity, dto.DisplayColor });
        return response.Success;
    }

    public async Task<bool> UpdateTagAsync(int id, AdminTagDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/admin/tags/{id}",
            new { Name = dto.TagName, dto.Category, dto.TargetEntity, dto.DisplayColor });
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

    public Task<PagedResult<AdminJobDto>?> GetJobsAsync(int page = 1)
        => _api.GetAsync<PagedResult<AdminJobDto>>($"/api/admin/jobs?page={page}");

    public Task<AdminNcfStatusDto?> GetNcfStatusAsync()
        => _api.GetAsync<AdminNcfStatusDto>("/api/admin/ncf");

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

    public async Task<(bool Success, string? ErrorMessage)> ScheduleNcfTrainingAsync(int priority = 10)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/admin/ncf-training/schedule", new { Priority = priority });
        return (response.Success, response.Error?.Message);
    }

    public Task<PagedResult<AdminIngredientSuggestionDto>?> GetIngredientSuggestionsAsync(int page = 1, string? status = null)
        => _api.GetAsync<PagedResult<AdminIngredientSuggestionDto>>($"/api/admin/ingredient-suggestions?page={page}&status={status}");

    public async Task<bool> ReviewIngredientSuggestionAsync(int id, bool approve, string? adminNote = null,
        bool? isAllergen = null, bool? isVegetarian = null, bool? isVegan = null,
        bool? isGlutenFree = null, bool? isLactoseFree = null, string? iconUrl = null, string? iconBlurhash = null)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/admin/ingredient-suggestions/{id}/review",
            new { Approve = approve, AdminNote = adminNote,
                IsAllergen = isAllergen, IsVegetarian = isVegetarian, IsVegan = isVegan,
                IsGlutenFree = isGlutenFree, IsLactoseFree = isLactoseFree,
                IconUrl = iconUrl, IconBlurhash = iconBlurhash });
        return response.Success;
    }

    public async Task<AdminIngredientIconDto?> UploadIngredientIconAsync(Stream file, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(file);
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        var apiResponse = await _api.PostMultipartApiResponseAsync<AdminIngredientIconDto>("/api/admin/ingredients/icon", content);
        return apiResponse is { Success: true } ? apiResponse.Data : null;
    }

    public async Task<List<AdminHeroImageDto>> GetHeroImagesAsync()
        => await _api.GetAsync<List<AdminHeroImageDto>>("/api/admin/hero-images") ?? [];

    public async Task<bool> DeleteHeroImageAsync(Guid publicId)
        => await _api.DeleteAsync($"/api/admin/hero-images/{publicId}");

    public async Task<AdminHeroImageDto?> UploadHeroImageAsync(Stream file, string fileName, string? creditText)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(file);
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        if (!string.IsNullOrWhiteSpace(creditText))
            content.Add(new StringContent(creditText), "creditText");

        var apiResponse = await _api.PostMultipartApiResponseAsync<AdminHeroImageDto>("/api/admin/hero-images", content);
        return apiResponse is { Success: true } ? apiResponse.Data : null;
    }

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

    public async Task<AdminSystemNodesResponseDto?> GetSystemNodesAsync()
        => await _api.GetAsync<AdminSystemNodesResponseDto>("/api/admin/nodes");

    public async Task<GpuWakeResultDto?> WakeGpuAsync()
    {
        var response = await _api.PostApiResponseAsync<GpuWakeResultDto>("/api/admin/nodes/gpu/wake", null);
        return response.Success ? response.Data : null;
    }

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
