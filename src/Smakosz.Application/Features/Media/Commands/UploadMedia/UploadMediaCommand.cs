using ErrorOr;
using MediatR;
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

        if (entityType == MediaEntityType.Hero && !_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

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
}
