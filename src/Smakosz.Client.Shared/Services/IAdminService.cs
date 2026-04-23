using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IAdminService
{
    Task<AdminDashboardDto?> GetDashboardAsync();
    Task<PagedResult<AdminTicketDto>?> GetTicketsAsync(int page = 1, string? status = null, string? ticketType = null);
    Task<AdminTicketDetailDto?> GetTicketDetailAsync(int id);
    Task<bool> UpdateTicketStatusAsync(int id, string status);
    Task<bool> RespondToContactAsync(int id, string response);
    Task<PagedResult<AdminPhotoDto>?> GetPendingPhotosAsync(int page = 1);
    Task<bool> ModeratePhotoAsync(Guid id, bool approve, IReadOnlyList<string>? reasonCodes = null, string? moderatorNote = null);
    Task<PagedResult<AdminReviewDto>?> GetPendingReviewsAsync(int page = 1);
    Task<bool> ModerateReviewAsync(Guid id, bool approve, IReadOnlyList<string>? reasonCodes = null, string? moderatorNote = null);
    Task<PagedResult<AdminReportDto>?> GetReportsAsync(int page = 1, string? status = null);
    Task<bool> ResolveReportAsync(int id, string resolution);
    Task<PagedResult<AdminEditRequestDto>?> GetEditRequestsAsync(int page = 1, int? restaurantId = null);
    Task<bool> ProcessEditRequestAsync(int id, string action, string? reason = null);
    Task<PagedResult<AdminUserDto>?> GetUsersAsync(int page = 1, string? search = null);
    Task<AdminUserDto?> GetUserAsync(int userId);
    Task<bool> UpdateUserAsync(Guid publicId, string action);
    Task<bool> Disable2faForUserAsync(Guid publicId);
    Task<PagedResult<AdminRestaurantDto>?> GetRestaurantsAsync(int page = 1, string? search = null);
    Task<AdminRestaurantDetailDto?> GetRestaurantDetailAsync(int id);
    Task<bool> UpdateRestaurantAsync(Guid publicId, AdminRestaurantUpdateDto dto);
    Task<bool> ChangeRestaurantStatusAsync(Guid publicId, string status, string? reason);
    Task<PagedResult<AdminModerationLogDto>?> GetRestaurantModerationHistoryAsync(int restaurantId, int page = 1);
    Task<bool> VerifyRestaurantAsync(Guid publicId);
    Task<PagedResult<AdminIngredientDto>?> GetIngredientsAsync(int page = 1, string? search = null);
    Task<bool> CreateIngredientAsync(AdminIngredientDto dto);
    Task<bool> UpdateIngredientAsync(int id, AdminIngredientDto dto);
    Task<bool> DeleteIngredientAsync(int id);
    Task<PagedResult<AdminCityDto>?> GetCitiesAsync(int page = 1, string? search = null);
    Task<bool> CreateCityAsync(AdminCityDto dto);
    Task<bool> UpdateCityAsync(int id, AdminCityDto dto);
    Task<bool> DeleteCityAsync(int id);
    Task<PagedResult<AdminTagDto>?> GetTagsAsync(int page = 1, string? search = null);
    Task<bool> CreateTagAsync(AdminTagDto dto);
    Task<bool> UpdateTagAsync(int id, AdminTagDto dto);
    Task<bool> DeleteTagAsync(int id);
    Task<List<AdminSystemConfigDto>> GetSystemConfigAsync();
    Task<bool> UpdateSystemConfigAsync(string key, string value);
    Task<PagedResult<AdminLogEntryDto>?> GetLogsAsync(int page = 1, string? level = null);
    Task<List<AdminAiModelDto>> GetAiModelsAsync();
    Task<PagedResult<AdminJobDto>?> GetJobsAsync(int page = 1);
    Task<bool> TriggerJobAsync(int id);
    Task<bool> CreateJobAsync(CreateJobRequest request);
    Task<bool> CancelJobAsync(int id);
    Task<(bool Success, string? ErrorMessage)> ScheduleNcfTrainingAsync();
    Task<PagedResult<AdminIngredientSuggestionDto>?> GetIngredientSuggestionsAsync(int page = 1, string? status = null);
    Task<bool> ReviewIngredientSuggestionAsync(int id, bool approve, string? adminNote = null,
        bool? isAllergen = null, bool? isVegetarian = null, bool? isVegan = null,
        bool? isGlutenFree = null, bool? isLactoseFree = null, string? iconUrl = null);
    Task<List<AdminHeroImageDto>> GetHeroImagesAsync();
    Task<bool> DeleteHeroImageAsync(Guid publicId);
    Task<PagedResult<AdminAuditLogDto>?> GetAuditLogsAsync(int page = 1, string? tableName = null, int? recordId = null);
    Task<PagedResult<AdminSecurityLogDto>?> GetSecurityLogsAsync(int page = 1, string? eventType = null);
    Task<List<AdminSystemNodeDto>?> GetSystemNodesAsync();
    Task<PagedResult<AdminBannedIdentifierDto>?> GetBannedIdentifiersAsync(int page = 1, string? type = null, bool includeExpired = false);
    Task<bool> CreateBannedIdentifierAsync(AdminBannedIdentifierDto dto);
    Task<bool> UpdateBannedIdentifierAsync(int id, object dto);
    Task<bool> DeleteBannedIdentifierAsync(int id);
    Task<PagedResult<AdminForbiddenWordDto>?> GetForbiddenWordsAsync(int page = 1, string? search = null);
    Task<bool> CreateForbiddenWordAsync(AdminForbiddenWordDto dto);
    Task<bool> UpdateForbiddenWordAsync(int id, AdminForbiddenWordDto dto);
    Task<bool> DeleteForbiddenWordAsync(int id);
    Task<bool?> TestForbiddenWordAsync(string text, string[] categories);
    Task<PagedResult<AdminRejectionReasonDto>?> GetRejectionReasonsAsync(int page = 1, int pageSize = 100, string? category = null, bool includeInactive = false);
    Task<bool> CreateRejectionReasonAsync(AdminRejectionReasonDto dto);
    Task<bool> UpdateRejectionReasonAsync(string reasonCode, AdminRejectionReasonDto dto);
    Task<bool> DeleteRejectionReasonAsync(string reasonCode);
}
