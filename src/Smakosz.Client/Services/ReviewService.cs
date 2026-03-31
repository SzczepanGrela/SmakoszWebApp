using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class ReviewService : IReviewService
{
    private readonly SmakoszApiClient _api;

    public ReviewService(SmakoszApiClient api) => _api = api;

    public Task<ApiResponse<ReviewCardDto>> CreateAsync(CreateReviewDto dto)
        => _api.PostApiResponseAsync<ReviewCardDto>("/api/reviews", dto);

    public Task<ApiResponse<ReviewCardDto>> UpdateAsync(Guid publicId, UpdateReviewDto dto)
        => _api.PutApiResponseAsync<ReviewCardDto>($"/api/reviews/{publicId}", dto);

    public Task<ApiResponse<object>> DeleteAsync(Guid publicId)
        => _api.DeleteApiResponseAsync($"/api/reviews/{publicId}");

    public Task<PagedResult<ReviewCardDto>?> GetByDishAsync(string dishSlug, int page = 1, int pageSize = 10, string sortBy = "newest")
        => _api.GetAsync<PagedResult<ReviewCardDto>>($"/api/dishes/{dishSlug}/reviews?page={page}&pageSize={pageSize}&sortBy={sortBy}");

    public Task<PagedResult<ReviewCardDto>?> GetByUserAsync(string userSlug, int page = 1, int pageSize = 10)
        => _api.GetAsync<PagedResult<ReviewCardDto>>($"/api/users/{userSlug}/reviews?page={page}&pageSize={pageSize}");

    public Task<List<ReportReasonDto>?> GetReportReasonsAsync()
        => _api.GetAsync<List<ReportReasonDto>>("/api/reviews/report-reasons");

    public Task<ApiResponse<ToggleLikeResult>> ToggleLikeAsync(Guid publicId)
        => _api.PostApiResponseAsync<ToggleLikeResult>($"/api/reviews/{publicId}/like");

    public async Task<bool> ReportReviewAsync(Guid publicId, List<string> reasonCodes, string? description)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/reviews/{publicId}/report",
            new { ReasonCodes = reasonCodes, Description = description });
        return response.Success;
    }
}
