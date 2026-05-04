using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private const long MinFileSize = 1024;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IPublicConfigProvider _configProvider;
    private readonly IBusinessMetrics _metrics;
    private readonly ILogger<UploadMediaHandler> _logger;

    public UploadMediaHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        IPublicConfigProvider configProvider,
        IBusinessMetrics metrics,
        ILogger<UploadMediaHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _configProvider = configProvider;
        _metrics = metrics;
        _logger = logger;
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

        if (request.File.Length < MinFileSize)
            return DomainErrors.Media.FileTooSmall;

        if (request.File.Length > maxFileSize)
            return DomainErrors.Media.FileTooLarge;

        if (!await HasValidImageMagicBytesAsync(request.File, cancellationToken))
            return DomainErrors.Media.InvalidFormat;

        if (!Enum.TryParse<MediaEntityType>(request.EntityType, true, out var entityType))
            return DomainErrors.Media.InvalidFormat;

        if (entityType is MediaEntityType.User or MediaEntityType.Hero)
            return DomainErrors.Media.UseDedicatedEndpoint;

        if (entityType is MediaEntityType.Restaurant or MediaEntityType.Dish or MediaEntityType.Review)
        {
            if (!request.EntityId.HasValue || request.EntityId.Value <= 0)
                return DomainErrors.Media.EntityNotFound;

            var entityExists = entityType switch
            {
                MediaEntityType.Restaurant => await _db.Restaurants.AnyAsync(r => r.RestaurantId == request.EntityId.Value, cancellationToken),
                MediaEntityType.Dish => await _db.Dishes.AnyAsync(d => d.DishId == request.EntityId.Value, cancellationToken),
                MediaEntityType.Review => await _db.Reviews.AnyAsync(r => r.ReviewId == request.EntityId.Value, cancellationToken),
                _ => true
            };
            if (!entityExists)
                return DomainErrors.Media.EntityNotFound;
        }

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

        try
        {
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

            _logger.LogInformation(
                "Uploaded media asset {AssetId} for {EntityType}/{EntityId} by user {UserId}",
                asset.AssetId, entityType, request.EntityId, _currentUser.UserId);

            var target = entityType switch
            {
                MediaEntityType.Review => "review",
                MediaEntityType.Dish => "dish",
                MediaEntityType.Restaurant => "restaurant",
                MediaEntityType.User => "user",
                MediaEntityType.Hero => "hero",
                _ => "other"
            };
            _metrics.RecordPhotoUpload(target);

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
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Upload DB save failed for {Folder}/{Slug}, rolling back R2 object {Key}",
                folder, slug, result.Key);
            try
            {
                await _storage.DeleteAsync(result.Key, cancellationToken);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx,
                    "Compensating R2 delete also failed for {Key}", result.Key);
            }
            throw;
        }
    }

    private static async Task<bool> HasValidImageMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        if (!stream.CanSeek)
            return true;

        var originalPosition = stream.Position;
        var buffer = new byte[12];
        var read = await stream.ReadAsync(buffer.AsMemory(0, 12), ct);
        stream.Position = originalPosition;

        if (read < 4)
            return false;

        // JPEG: FF D8 FF
        if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            return true;
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            return true;
        // WEBP: "RIFF" .... "WEBP"
        if (read >= 12
            && buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46
            && buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50)
            return true;

        return false;
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
