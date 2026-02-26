using System.Text.Json;
using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Media.Commands.UploadMedia;

public record UploadMediaCommand(
    Stream File,
    string FileName,
    string EntityType,
    int? EntityId
) : IRequest<ErrorOr<UploadMediaResult>>;

public class UploadMediaResult
{
    public long AssetId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbUrl { get; set; }
    public string? TinyUrl { get; set; }
    public string? HeroUrl { get; set; }
    public string? Blurhash { get; set; }
}

public class UploadMediaHandler : IRequestHandler<UploadMediaCommand, ErrorOr<UploadMediaResult>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp" };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public UploadMediaHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IFileStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<ErrorOr<UploadMediaResult>> Handle(UploadMediaCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var ext = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(ext))
            return DomainErrors.Media.InvalidFormat;

        if (request.File.Length > MaxFileSize)
            return DomainErrors.Media.FileTooLarge;

        if (!Enum.TryParse<MediaEntityType>(request.EntityType, true, out var entityType))
            return DomainErrors.Media.InvalidFormat;

        var folder = $"smakosz/images/{request.EntityType.ToLowerInvariant()}";
        var slug = $"{Guid.NewGuid():N}";

        var result = await _storage.UploadAsync(request.File, slug, folder, cancellationToken);

        var asset = new MediaAsset
        {
            EntityType = entityType,
            EntityId = request.EntityId ?? 0,
            Url = result.PublicUrl,
            Blurhash = result.Blurhash,
            Status = MediaAssetStatus.Pending,
            UploadedBy = _currentUser.UserId.Value
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        _db.SystemJobs.Add(new SystemJob
        {
            Type = "image_moderation",
            Status = JobStatus.Pending,
            Priority = 5,
            EntityId = asset.AssetId.ToString(),
            EntityType = "media_asset",
            Payload = JsonSerializer.Serialize(new
            {
                asset_id = asset.AssetId,
                image_url = asset.Url
            })
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new UploadMediaResult
        {
            AssetId = asset.AssetId,
            Url = result.PublicUrl,
            ThumbUrl = result.ThumbUrl,
            TinyUrl = result.TinyUrl,
            HeroUrl = result.HeroUrl,
            Blurhash = result.Blurhash
        };
    }
}
