using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessDishDetail;

public record GetBusinessDishDetailQuery(Guid PublicId) : IRequest<ErrorOr<BusinessDishDetailDto>>;

public class BusinessDishDetailDto
{
    public Guid PublicId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public int? Calories { get; set; }
    public bool IsAvailable { get; set; }
    public string? ImageUrl { get; set; }
    public string? IngredientsJson { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsLactoseFree { get; set; }
    public bool IsSpicy { get; set; }
    public int ReviewCount { get; set; }
    public double? AvgRating { get; set; }
}

public class GetBusinessDishDetailHandler : IRequestHandler<GetBusinessDishDetailQuery, ErrorOr<BusinessDishDetailDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessDishDetailHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<BusinessDishDetailDto>> Handle(GetBusinessDishDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var dish = await _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Where(d => d.PublicId == request.PublicId)
            .Select(d => new
            {
                d.PublicId,
                d.DishName,
                d.Slug,
                d.Price,
                d.Description,
                d.Calories,
                d.IsAvailable,
                d.ImageUrl,
                d.IngredientsJson,
                d.IsVegetarian,
                d.IsVegan,
                d.IsGlutenFree,
                d.IsLactoseFree,
                d.IsSpicy,
                d.ReviewCount,
                d.AvgRating,
                OwnerId = d.Restaurant!.OwnerId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        if (dish.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.Business.NotOwner;

        return new BusinessDishDetailDto
        {
            PublicId = dish.PublicId,
            DishName = dish.DishName,
            Slug = dish.Slug,
            Price = dish.Price,
            Description = dish.Description,
            Calories = dish.Calories,
            IsAvailable = dish.IsAvailable,
            ImageUrl = dish.ImageUrl,
            IngredientsJson = dish.IngredientsJson,
            IsVegetarian = dish.IsVegetarian,
            IsVegan = dish.IsVegan,
            IsGlutenFree = dish.IsGlutenFree,
            IsLactoseFree = dish.IsLactoseFree,
            IsSpicy = dish.IsSpicy,
            ReviewCount = dish.ReviewCount,
            AvgRating = dish.AvgRating
        };
    }
}
