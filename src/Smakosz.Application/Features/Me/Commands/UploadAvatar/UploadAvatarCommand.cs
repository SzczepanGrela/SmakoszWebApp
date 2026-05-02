using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.UploadAvatar;

public record UploadAvatarCommand(Stream File, string FileName)
    : IRequest<ErrorOr<UploadAvatarResult>>;

public record UploadAvatarResult(string Url, string? Blurhash);

public class UploadAvatarHandler : IRequestHandler<UploadAvatarCommand, ErrorOr<UploadAvatarResult>>
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private const double TargetRatio = 1.0;
    private const string Bucket = "smakosz-photos";

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IImageProcessingService _imageProcessor;
    private readonly IPublicConfigProvider _configProvider;
    private readonly IBusinessMetrics _metrics;

    public UploadAvatarHandler(
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

    public async Task<ErrorOr<UploadAvatarResult>> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var ext = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(ext))
            return DomainErrors.Media.InvalidFormat;

        var maxSizeMb = await _configProvider.GetIntAsync("upload.avatar_max_size_mb", 1, cancellationToken);
        var maxFileSize = maxSizeMb * 1024L * 1024L;
        if (request.File.Length > maxFileSize)
            return DomainErrors.Media.FileTooLarge;

        var ratioResult = await ImageDimensionValidator.ValidateRatioAsync(request.File, _imageProcessor, TargetRatio);
        if (ratioResult.IsError)
            return ratioResult.Errors;

        if (request.File.CanSeek) request.File.Position = 0;

        var userId = _currentUser.UserId.Value;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
            return DomainErrors.Auth.InvalidCredentials;

        var folder = "uploads/user";
        var slug = $"{Guid.NewGuid():N}";
        var variants = ImageVariants.ForEntityType(MediaEntityType.User);
        var uploadResult = await _storage.UploadAsync(request.File, slug, folder, variants, cancellationToken);

        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            var previousAsset = await _db.MediaAssets
                .FirstOrDefaultAsync(a => a.EntityType == MediaEntityType.User && a.EntityId == userId, cancellationToken);

            EnqueueR2Cleanup(user.AvatarUrl, "avatar_replaced", userId);

            if (previousAsset is not null)
                _db.MediaAssets.Remove(previousAsset);
        }

        user.AvatarUrl = uploadResult.PublicUrl;
        user.AvatarBlurhash = uploadResult.Blurhash;

        var asset = new MediaAsset
        {
            EntityType = MediaEntityType.User,
            EntityId = userId,
            Url = uploadResult.PublicUrl,
            Blurhash = uploadResult.Blurhash,
            Width = uploadResult.Width,
            Height = uploadResult.Height,
            ModerationStatus = ContentModerationStatus.Approved,
            UploadedBy = userId
        };
        _db.MediaAssets.Add(asset);

        await _db.SaveChangesAsync(cancellationToken);

        if (!ModerationPolicyHelper.IsAutoApproved(_currentUser))
        {
            _db.SystemTickets.Add(new SystemTicket
            {
                TicketType = TicketType.Photo,
                ReferenceId = asset.AssetId,
                Status = TicketStatus.Open,
                Priority = 3,
                Description = "Nowy avatar użytkownika wymaga moderacji"
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        _metrics.RecordPhotoUpload("user");

        return new UploadAvatarResult(uploadResult.PublicUrl, uploadResult.Blurhash);
    }

    private void EnqueueR2Cleanup(string url, string reason, int userId)
    {
        try
        {
            var key = new Uri(url).AbsolutePath.TrimStart('/');
            _db.FilesToDelete.Add(new FileToDelete
            {
                R2Key = key,
                Bucket = Bucket,
                Reason = reason,
                SourceEntity = "User",
                SourceId = userId,
                QueuedAt = DateTime.UtcNow
            });
        }
        catch (UriFormatException)
        {
        }
    }
}
