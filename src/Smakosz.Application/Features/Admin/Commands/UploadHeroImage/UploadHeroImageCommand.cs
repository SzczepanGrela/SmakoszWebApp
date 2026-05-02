using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UploadHeroImage;

public record UploadHeroImageCommand(Stream File, string FileName, string? CreditText)
    : IRequest<ErrorOr<UploadHeroImageResult>>;

public record UploadHeroImageResult(Guid PublicId, string Url, string? Blurhash, string? CreditText, DateTime? CreatedAt);

public class UploadHeroImageHandler : IRequestHandler<UploadHeroImageCommand, ErrorOr<UploadHeroImageResult>>
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private const double TargetRatio = 21.0 / 9.0;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IImageProcessingService _imageProcessor;
    private readonly IPublicConfigProvider _configProvider;
    private readonly IBusinessMetrics _metrics;

    public UploadHeroImageHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        IImageProcessingService imageProcessor,
        IPublicConfigProvider configProvider,
        IBusinessMetrics metrics)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _imageProcessor = imageProcessor;
        _configProvider = configProvider;
        _metrics = metrics;
    }

    public async Task<ErrorOr<UploadHeroImageResult>> Handle(UploadHeroImageCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var ext = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(ext))
            return DomainErrors.Media.InvalidFormat;

        var maxSizeMb = await _configProvider.GetIntAsync("upload.hero_max_size_mb", 5, cancellationToken);
        var maxFileSize = maxSizeMb * 1024L * 1024L;
        if (request.File.Length > maxFileSize)
            return DomainErrors.Media.FileTooLarge;

        var ratioResult = await ImageDimensionValidator.ValidateRatioAsync(request.File, _imageProcessor, TargetRatio);
        if (ratioResult.IsError)
            return ratioResult.Errors;

        if (request.File.CanSeek) request.File.Position = 0;

        var folder = "uploads/hero";
        var slug = $"{Guid.NewGuid():N}";
        var variants = ImageVariants.ForEntityType(MediaEntityType.Hero);
        var uploadResult = await _storage.UploadAsync(request.File, slug, folder, variants, cancellationToken);

        var asset = new MediaAsset
        {
            EntityType = MediaEntityType.Hero,
            EntityId = 0,
            Url = uploadResult.PublicUrl,
            Blurhash = uploadResult.Blurhash,
            Width = uploadResult.Width,
            Height = uploadResult.Height,
            ModerationStatus = ContentModerationStatus.Approved,
            UploadedBy = _currentUser.UserId.Value,
            CreditText = request.CreditText
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        _metrics.RecordPhotoUpload("hero");

        return new UploadHeroImageResult(asset.PublicId, asset.Url, asset.Blurhash, asset.CreditText, asset.CreatedAt);
    }
}
