using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IBusinessService
{
    Task<BusinessDashboardDto?> GetDashboardAsync();
    Task<BusinessRestaurantDto?> GetRestaurantInfoAsync();
    Task<bool> UpdateRestaurantInfoAsync(BusinessRestaurantDto dto);
    Task<List<OpeningHoursDto>> GetOpeningHoursAsync();
    Task<bool> UpdateOpeningHoursAsync(List<OpeningHoursDto> hours);
    Task<List<MenuSectionDto>> GetMenuSectionsAsync();
    Task<bool> UpdateMenuSectionsAsync(List<MenuSectionDto> sections);
    Task<PagedResult<BusinessDishDto>?> GetDishesAsync(int page = 1);
    Task<DishDetailDto?> GetDishAsync(Guid id);
    Task<bool> CreateDishAsync(DishDetailDto dish);
    Task<bool> UpdateDishAsync(Guid id, DishDetailDto dish);
    Task<bool> DeleteDishAsync(Guid id);
    Task<PagedResult<ReviewCardDto>?> GetReviewsAsync(int page = 1);
    Task<BusinessStatsDto?> GetStatsAsync();
    Task<List<EditRequestSummaryDto>> GetEditRequestsAsync();
    Task<RegistrationStatusDto?> GetRegistrationStatusAsync();
    Task<bool> RegisterBusinessAsync(BusinessRestaurantDto dto);
    Task<bool> CreateMenuSectionAsync(string name);
    Task<bool> UpdateMenuSectionAsync(int sectionId, string name);
    Task<bool> DeleteMenuSectionAsync(int sectionId);
    Task<bool> UpdateDishAvailabilityAsync(Guid publicId, bool isAvailable);
    Task<bool> CreateEditRequestAsync(CreateEditRequestDto dto);
    Task<bool> SetDishIngredientsAsync(Guid publicId, List<int> ingredientIds);
}

public record CreateEditRequestDto(
    string ChangeType, string? Payload, string? NewName, string? NewDescription,
    string? NewAddress, string? NewPhone, string? NewWebsite);
