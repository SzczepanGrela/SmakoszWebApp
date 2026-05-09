using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UploadIngredientIcon;

public record UploadIngredientIconCommand(Stream File, string FileName)
    : IRequest<ErrorOr<UploadIngredientIconResult>>;

public record UploadIngredientIconResult(string IconUrl, string? IconBlurhash);

public class UploadIngredientIconHandler : IRequestHandler<UploadIngredientIconCommand, ErrorOr<UploadIngredientIconResult>>
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IPublicConfigProvider _configProvider;
    private readonly ILogger<UploadIngredientIconHandler> _logger;

    public UploadIngredientIconHandler(
        ICurrentUserService currentUser,
        IFileStorageService storage,
        IPublicConfigProvider configProvider,
        ILogger<UploadIngredientIconHandler> logger)
    {
        _currentUser = currentUser;
        _storage = storage;
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<UploadIngredientIconResult>> Handle(UploadIngredientIconCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var ext = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(ext))
            return DomainErrors.Media.InvalidFormat;

        var maxSizeMb = await _configProvider.GetIntAsync("upload.max_size_mb", 5, cancellationToken);
        var maxFileSize = maxSizeMb * 1024L * 1024L;
        if (request.File.Length > maxFileSize)
            return DomainErrors.Media.FileTooLarge;

        if (request.File.CanSeek) request.File.Position = 0;

        var folder = "uploads/ingredients";
        var slug = $"{Guid.NewGuid():N}";
        var variants = ImageVariants.ForEntityType(MediaEntityType.Ingredient);
        var uploadResult = await _storage.UploadAsync(request.File, slug, folder, variants, cancellationToken);

        _logger.LogInformation("Uploaded ingredient icon {Url} by admin {UserId}", uploadResult.PublicUrl, _currentUser.UserId);

        return new UploadIngredientIconResult(uploadResult.PublicUrl, uploadResult.Blurhash);
    }
}
