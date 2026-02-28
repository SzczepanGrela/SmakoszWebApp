using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetHeroImages;

public record GetHeroImagesQuery : IRequest<ErrorOr<List<HeroImageItemDto>>>;

public class HeroImageItemDto
{
    public Guid PublicId { get; init; }
    public string Url { get; init; } = string.Empty;
    public string? Blurhash { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public class GetHeroImagesHandler : IRequestHandler<GetHeroImagesQuery, ErrorOr<List<HeroImageItemDto>>>
{
    private readonly ISmakoszDbContext _db;

    public GetHeroImagesHandler(ISmakoszDbContext db) => _db = db;

    public async Task<ErrorOr<List<HeroImageItemDto>>> Handle(GetHeroImagesQuery request, CancellationToken cancellationToken)
    {
        var images = await _db.MediaAssets
            .AsNoTracking()
            .Where(m => m.EntityType == MediaEntityType.Hero)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new HeroImageItemDto
            {
                PublicId = m.PublicId,
                Url = m.Url,
                Blurhash = m.Blurhash,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return images;
    }
}
