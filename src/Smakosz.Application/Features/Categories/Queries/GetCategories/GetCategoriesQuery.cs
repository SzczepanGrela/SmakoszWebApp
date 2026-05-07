using System.Globalization;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Categories.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Categories.Queries.GetCategories;

public record GetCategoriesQuery : IRequest<ErrorOr<List<CategoryDto>>>;

public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, ErrorOr<List<CategoryDto>>>
{
    private static readonly StringComparer PolishComparer =
        StringComparer.Create(new CultureInfo("pl-PL"), ignoreCase: true);

    private readonly ISmakoszDbContext _db;

    public GetCategoriesHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<List<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _db.Restaurants
            .AsNoTracking()
            .Where(r => r.Status == RestaurantStatus.Active
                && (r.ModerationStatus == ContentModerationStatus.None || r.ModerationStatus == ContentModerationStatus.Approved)
                && r.Cuisine != null)
            .GroupBy(r => new { r.Cuisine!.DisplayName, r.Cuisine.Icon })
            .Select(g => new CategoryDto
            {
                Name = g.Key.DisplayName,
                Icon = g.Key.Icon,
                RestaurantCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        return categories.OrderBy(c => c.Name, PolishComparer).ToList();
    }
}
