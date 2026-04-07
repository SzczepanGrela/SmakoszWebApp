using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Media.Commands.UploadMedia;

public record UploadMediaCommand(
    Stream File,
    string FileName,
    string EntityType,
    int? EntityId,
    string? CreditText = null
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
    private readonly IPublicConfigProvider _configProvider;

    public UploadMediaHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IFileStorageService storage, IPublicConfigProvider configProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _configProvider = configProvider;
    }

    public async Task<ErrorOr<UploadMediaResult>> Handle(UploadMediaCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var maxSizeMb = await _configProvider.GetIntAsync("upload.max_size_mb", 5, cancellationToken);
        var allowedTypesRaw = await _configProvider.GetValueAsync("upload.allowed_types", cancellationToken);
        var allowedExtensions = ParseAllowedExtensions(allowedTypesRaw);
        var maxFileSize = maxSizeMb * 1024L * 1024L;

        var ext = Path.GetExtension(request.FileName);
        if (!allowedExtensions.Contains(ext))
            return DomainErrors.Media.InvalidFormat;

        if (request.File.Length > maxFileSize)
            return DomainErrors.Media.FileTooLarge;

        if (!Enum.TryParse<MediaEntityType>(request.EntityType, true, out var entityType))
            return DomainErrors.Media.InvalidFormat;

        if (entityType == MediaEntityType.Hero && !_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        if (entityType == MediaEntityType.Review && request.EntityId.HasValue)
        {
            var maxPhotos = await _configProvider.GetIntAsync("upload.max_photos_per_review", 5, cancellationToken);
            var currentCount = await _db.MediaAssets
                .CountAsync(a => a.EntityType == MediaEntityType.Review && a.EntityId == request.EntityId.Value, cancellationToken);
            if (currentCount >= maxPhotos)
                return DomainErrors.Media.PhotoLimitExceeded;
        }

        var folder = $"uploads/{request.EntityType.ToLowerInvariant()}";
        var slug = $"{Guid.NewGuid():N}";

        var variants = ImageVariants.ForEntityType(entityType);
        var result = await _storage.UploadAsync(request.File, slug, folder, variants, cancellationToken);

        var asset = new MediaAsset
        {
            EntityType = entityType,
            EntityId = request.EntityId ?? 0,
            Url = result.PublicUrl,
            Blurhash = result.Blurhash,
            Width = result.Width,
            Height = result.Height,
            ModerationStatus = ContentModerationStatus.Pending,
            UploadedBy = _currentUser.UserId.Value,
            CreditText = request.CreditText
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        if (entityType != MediaEntityType.Hero)
        {
            _db.SystemTickets.Add(new SystemTicket
            {
                TicketType = TicketType.Photo,
                ReferenceId = asset.AssetId,
                Status = TicketStatus.Open,
                Priority = 3,
                Description = $"Nowe zdjęcie ({entityType}) wymaga moderacji"
            });
        }

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

    private static HashSet<string> ParseAllowedExtensions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        return new HashSet<string>(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}
