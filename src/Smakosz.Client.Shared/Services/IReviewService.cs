using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IReviewService
{
    Task<ApiResponse<ReviewCardDto>> CreateAsync(CreateReviewDto dto);
    Task<ApiResponse<ReviewCardDto>> UpdateAsync(Guid publicId, UpdateReviewDto dto);
    Task<ApiResponse<object>> DeleteAsync(Guid publicId);
    Task<PagedResult<ReviewCardDto>?> GetByDishAsync(string dishSlug, int page = 1, int pageSize = 10, string sortBy = "newest");
    Task<PagedResult<ReviewCardDto>?> GetByUserAsync(string userSlug, int page = 1, int pageSize = 10);
    Task<bool> ReportReviewAsync(Guid publicId, string reason, string? description);
}
