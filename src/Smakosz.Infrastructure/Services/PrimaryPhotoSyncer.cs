using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class PrimaryPhotoSyncer : IPrimaryPhotoSyncer
{
    private readonly ISmakoszDbContext _db;

    public PrimaryPhotoSyncer(ISmakoszDbContext db) => _db = db;

    public async Task SyncToEntityAsync(long assetId, CancellationToken ct)
    {
        var asset = await _db.MediaAssets
            .Where(m => m.AssetId == assetId)
            .Select(m => new { m.AssetId, m.EntityType, m.EntityId, m.IsPrimary, m.ModerationStatus, m.Url, m.Blurhash })
            .FirstOrDefaultAsync(ct);
        if (asset is null || !asset.IsPrimary || asset.ModerationStatus != ContentModerationStatus.Approved)
            return;

        switch (asset.EntityType)
        {
            case MediaEntityType.Restaurant:
                await _db.Restaurants
                    .Where(r => r.RestaurantId == asset.EntityId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.ImageUrl, asset.Url)
                        .SetProperty(r => r.ImageBlurhash, asset.Blurhash), ct);
                break;
            case MediaEntityType.Dish:
                await _db.Dishes
                    .Where(d => d.DishId == asset.EntityId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(d => d.ImageUrl, asset.Url)
                        .SetProperty(d => d.ImageBlurhash, asset.Blurhash), ct);
                break;
            case MediaEntityType.User:
                await _db.Users
                    .Where(u => u.UserId == asset.EntityId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.AvatarUrl, asset.Url)
                        .SetProperty(u => u.AvatarBlurhash, asset.Blurhash), ct);
                break;
        }
    }
}
