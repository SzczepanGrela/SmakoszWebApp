using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetAdminRestaurantDetail;

public record GetAdminRestaurantDetailQuery(int RestaurantId) : IRequest<ErrorOr<AdminRestaurantDetailDto>>;

public class GetAdminRestaurantDetailHandler : IRequestHandler<GetAdminRestaurantDetailQuery, ErrorOr<AdminRestaurantDetailDto>>
{
    private const int RecentReviewsLimit = 5;
    private const int ContentPreviewLength = 160;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminRestaurantDetailHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<AdminRestaurantDetailDto>> Handle(GetAdminRestaurantDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var detail = await _db.Restaurants
            .AsNoTracking()
            .Where(r => r.RestaurantId == request.RestaurantId)
            .Select(r => new AdminRestaurantDetailDto
            {
                RestaurantId = r.RestaurantId,
                PublicId = r.PublicId,
                Name = r.RestaurantName,
                Slug = r.Slug,
                Description = r.Description,
                CuisineTypeId = r.CuisineTypeId,
                CuisineType = r.Cuisine != null ? r.Cuisine.DisplayName : null,
                PriceLevel = r.PriceLevel,
                ImageUrl = r.ImageUrl,
                ImageBlurhash = r.ImageBlurhash,

                Address = r.Address,
                PostalCode = r.PostalCode,
                Phone = r.Phone,
                Email = r.Email,
                Website = r.Website,
                CityId = r.CityId,
                CityName = r.City != null ? r.City.CityName : null,

                OwnerId = r.OwnerId,
                OwnerPublicId = r.Owner != null ? r.Owner.PublicId : (Guid?)null,
                OwnerUsername = r.Owner != null ? r.Owner.Username : null,
                OwnerEmail = r.Owner != null ? r.Owner.Email : null,

                Status = r.Status.ToString(),
                IsVerified = r.IsVerified,
                ModerationStatus = r.ModerationStatus.ToString(),
                VerifiedAt = r.VerifiedAt,
                VerifiedByUsername = r.VerifiedBy != null
                    ? _db.Users.Where(u => u.UserId == r.VerifiedBy).Select(u => u.Username).FirstOrDefault()
                    : null,
                Version = r.Version,

                AvgFoodScore = r.AvgFoodScore,
                AvgServiceScore = r.AvgService,
                AvgCleanlinessScore = r.AvgCleanliness,
                AvgAmbianceScore = r.AvgAmbiance,
                TrendingScore = r.TrendingScore,
                ReviewCount = _db.Reviews.Count(rv => rv.RestaurantId == r.RestaurantId && !rv.IsDeleted),

                PendingEditRequestsCount = _db.RestaurantEditRequests
                    .Count(er => er.RestaurantId == r.RestaurantId && er.Status == EditRequestStatus.Pending),
                PendingPhotosCount = _db.MediaAssets
                    .Count(m => m.EntityType == MediaEntityType.Restaurant
                                && m.EntityId == r.RestaurantId
                                && m.ModerationStatus == ContentModerationStatus.Pending),
                ApprovedPhotosCount = _db.MediaAssets
                    .Count(m => m.EntityType == MediaEntityType.Restaurant
                                && m.EntityId == r.RestaurantId
                                && m.ModerationStatus == ContentModerationStatus.Approved),
                MenuSectionsCount = _db.MenuSections.Count(s => s.RestaurantId == r.RestaurantId),
                MenuItemsCount = _db.Dishes.Count(d => d.RestaurantId == r.RestaurantId),

                OpeningHours = _db.RestaurantOpeningHours
                    .Where(oh => oh.RestaurantId == r.RestaurantId)
                    .OrderBy(oh => oh.DayOfWeek)
                    .Select(oh => new AdminRestaurantOpeningHoursDto
                    {
                        DayOfWeek = oh.DayOfWeek,
                        OpenTime = oh.OpenTime,
                        CloseTime = oh.CloseTime,
                        IsClosed = oh.IsClosed
                    })
                    .ToList(),

                RecentReviews = _db.Reviews
                    .Where(rv => rv.RestaurantId == r.RestaurantId && !rv.IsDeleted)
                    .OrderByDescending(rv => rv.CreatedAt)
                    .Take(RecentReviewsLimit)
                    .Select(rv => new AdminRestaurantReviewSummaryDto
                    {
                        ReviewId = rv.ReviewId,
                        PublicId = rv.PublicId,
                        Username = rv.User != null ? rv.User.Username : null,
                        DishName = rv.Dish != null ? rv.Dish.DishName : null,
                        DishRating = rv.DishRating,
                        ContentPreview = rv.Content != null && rv.Content.Length > ContentPreviewLength
                            ? rv.Content.Substring(0, ContentPreviewLength) + "..."
                            : rv.Content,
                        ModerationStatus = rv.ModerationStatus.ToString(),
                        CreatedAt = rv.CreatedAt
                    })
                    .ToList(),

                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
            return DomainErrors.Restaurant.NotFound;

        return detail;
    }
}
