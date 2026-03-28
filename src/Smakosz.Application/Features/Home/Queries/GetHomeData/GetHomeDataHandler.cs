using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Application.Features.Home.Dtos;
using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Application.Features.Reviews.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Home.Queries.GetHomeData;

public class GetHomeDataHandler : IRequestHandler<GetHomeDataQuery, ErrorOr<HomeDataDto>>
{
    private readonly ISmakoszDbContext _db;

    public GetHomeDataHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<HomeDataDto>> Handle(GetHomeDataQuery request, CancellationToken cancellationToken)
    {
        var siteStats = await _db.SiteStats.AsNoTracking().FirstAsync(cancellationToken);
        var stats = new StatsDto
        {
            TotalDishes = siteStats.TotalDishes,
            TotalRestaurants = siteStats.TotalRestaurants,
            TotalReviews = siteStats.TotalReviews
        };

        var trendingRestaurants = await _db.Restaurants
            .AsNoTracking()
            .Include(r => r.City)
            .Where(r => r.Status == RestaurantStatus.Active)
            .OrderByDescending(r => r.TrendingScore)
            .Take(6)
            .Select(r => new RestaurantCardDto
            {
                PublicId = r.PublicId,
                Slug = r.Slug ?? string.Empty,
                RestaurantName = r.RestaurantName,
                CuisineType = r.CuisineType,
                CityName = r.City != null ? r.City.CityName : null,
                PriceLevel = r.PriceLevel,
                AvgFoodScore = r.AvgFoodScore,
                ReviewCount = 0,
                ImageUrl = r.ImageUrl,
                ImageBlurhash = r.ImageBlurhash,
                IsFavorite = false
            })
            .ToListAsync(cancellationToken);

        var trendingDishes = await _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Where(d => d.IsAvailable && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active)
            .OrderByDescending(d => d.TrendingScore)
            .Take(12)
            .Select(d => new DishCardDto
            {
                PublicId = d.PublicId,
                Slug = d.Slug ?? string.Empty,
                DishName = d.DishName,
                Price = d.Price,
                AvgRating = d.AvgRating,
                ReviewCount = d.ReviewCount,
                ImageUrl = d.ImageUrl,
                ImageBlurhash = d.ImageBlurhash,
                RestaurantName = d.Restaurant != null ? d.Restaurant.RestaurantName : null,
                RestaurantSlug = d.Restaurant != null ? d.Restaurant.Slug : null,
                IsVegetarian = d.IsVegetarian,
                IsVegan = d.IsVegan,
                IsGlutenFree = d.IsGlutenFree,
                IsSaved = false
            })
            .ToListAsync(cancellationToken);

        var topRatedDishes = await _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Where(d => d.IsAvailable && d.ReviewCount >= 3 && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active)
            .OrderByDescending(d => d.AvgRating)
            .Take(12)
            .Select(d => new DishCardDto
            {
                PublicId = d.PublicId,
                Slug = d.Slug ?? string.Empty,
                DishName = d.DishName,
                Price = d.Price,
                AvgRating = d.AvgRating,
                ReviewCount = d.ReviewCount,
                ImageUrl = d.ImageUrl,
                ImageBlurhash = d.ImageBlurhash,
                RestaurantName = d.Restaurant != null ? d.Restaurant.RestaurantName : null,
                RestaurantSlug = d.Restaurant != null ? d.Restaurant.Slug : null,
                IsVegetarian = d.IsVegetarian,
                IsVegan = d.IsVegan,
                IsGlutenFree = d.IsGlutenFree,
                IsSaved = false
            })
            .ToListAsync(cancellationToken);

        var recentReviews = await _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Dish)
            .Include(r => r.Restaurant)
            .Where(r => !r.IsDeleted && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt)
            .Take(6)
            .Select(r => new ReviewCardDto
            {
                PublicId = r.PublicId,
                DishRating = r.DishRating,
                ServiceRating = r.ServiceRating,
                CleanlinessRating = r.CleanlinessRating,
                AmbianceRating = r.AmbianceRating,
                Content = r.Content,
                ContentStatus = r.ModerationStatus,
                VisitDate = r.VisitDate,
                HelpfulCount = r.HelpfulCount,
                IsHelpfulByMe = false,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                Author = new UserSummaryDto
                {
                    PublicId = r.User.PublicId,
                    Slug = r.User.Slug ?? string.Empty,
                    Username = r.User.Username,
                    AvatarUrl = r.User.AvatarUrl,
                    AvatarBlurhash = r.User.AvatarBlurhash,
                    ReviewCount = r.User.ReviewCount
                },
                DishName = r.Dish.DishName,
                DishSlug = r.Dish.Slug ?? string.Empty,
                RestaurantName = r.Restaurant.RestaurantName,
                RestaurantSlug = r.Restaurant.Slug ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var popularCategories = await _db.Restaurants
            .AsNoTracking()
            .Where(r => r.Status == RestaurantStatus.Active && r.CuisineType != null)
            .GroupBy(r => r.CuisineType!)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        var heroImage = await _db.MediaAssets
            .AsNoTracking()
            .Where(m => m.EntityType == MediaEntityType.Hero && m.ModerationStatus == ContentModerationStatus.Approved)
            .OrderBy(_ => EF.Functions.Random())
            .Select(m => new HeroImageDto { Url = m.Url, Blurhash = m.Blurhash, CreditText = m.CreditText })
            .FirstOrDefaultAsync(cancellationToken);

        return new HomeDataDto
        {
            Stats = stats,
            TrendingRestaurants = trendingRestaurants,
            TrendingDishes = trendingDishes,
            TopRatedDishes = topRatedDishes,
            RecentReviews = recentReviews,
            PopularCategories = popularCategories,
            HeroImage = heroImage
        };
    }
}
