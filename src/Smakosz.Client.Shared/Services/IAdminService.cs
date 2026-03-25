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
    Task<bool> ModeratePhotoAsync(Guid id, string action, string? reason = null);
    Task<PagedResult<AdminReviewDto>?> GetPendingReviewsAsync(int page = 1);
    Task<bool> ModerateReviewAsync(Guid id, string action, string? reason = null);
    Task<PagedResult<AdminReportDto>?> GetReportsAsync(int page = 1, string? status = null);
    Task<bool> ResolveReportAsync(int id, string resolution);
    Task<PagedResult<AdminEditRequestDto>?> GetEditRequestsAsync(int page = 1);
    Task<bool> ProcessEditRequestAsync(int id, string action, string? reason = null);
    Task<PagedResult<AdminUserDto>?> GetUsersAsync(int page = 1, string? search = null);
    Task<AdminUserDto?> GetUserAsync(Guid publicId);
    Task<bool> UpdateUserAsync(Guid publicId, string action);
    Task<PagedResult<RestaurantCardDto>?> GetRestaurantsAsync(int page = 1, string? search = null);
    Task<PagedResult<AdminIngredientDto>?> GetIngredientsAsync(int page = 1, string? search = null);
    Task<bool> CreateIngredientAsync(AdminIngredientDto dto);
    Task<bool> UpdateIngredientAsync(int id, AdminIngredientDto dto);
    Task<bool> DeleteIngredientAsync(int id);
    Task<PagedResult<AdminCityDto>?> GetCitiesAsync(int page = 1, string? search = null);
    Task<bool> CreateCityAsync(AdminCityDto dto);
    Task<bool> UpdateCityAsync(int id, AdminCityDto dto);
    Task<bool> DeleteCityAsync(int id);
    Task<List<AdminSystemConfigDto>> GetSystemConfigAsync();
    Task<bool> UpdateSystemConfigAsync(string key, string value);
    Task<PagedResult<AdminLogEntryDto>?> GetLogsAsync(int page = 1, string? level = null);
    Task<List<AdminAiModelDto>> GetAiModelsAsync();
    Task<PagedResult<AdminJobDto>?> GetJobsAsync(int page = 1);
    Task<bool> TriggerJobAsync(int id);
    Task<PagedResult<AdminIngredientSuggestionDto>?> GetIngredientSuggestionsAsync(int page = 1, string? status = null);
    Task<bool> ReviewIngredientSuggestionAsync(int id, bool approve, string? adminNote = null);
    Task<List<AdminHeroImageDto>> GetHeroImagesAsync();
    Task<bool> DeleteHeroImageAsync(Guid publicId);
}
