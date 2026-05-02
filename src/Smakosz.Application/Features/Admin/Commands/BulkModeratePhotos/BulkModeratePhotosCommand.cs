using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ModeratePhoto;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.BulkModeratePhotos;

public record BulkModeratePhotosCommand(
    IReadOnlyList<Guid> PublicIds,
    bool Approve,
    IReadOnlyList<string>? ReasonCodes,
    string? ModeratorNote) : IRequest<ErrorOr<BulkModerateResult>>;

public record BulkModerateResult(
    IReadOnlyList<Guid> Success,
    IReadOnlyList<BulkModerateFailure> Failed);

public record BulkModerateFailure(
    Guid PublicId,
    string ErrorCode,
    string Message);

public class BulkModeratePhotosValidator : AbstractValidator<BulkModeratePhotosCommand>
{
    public const int HardUpperBound = 50;

    public BulkModeratePhotosValidator()
    {
        RuleFor(x => x.PublicIds)
            .NotNull().NotEmpty()
            .WithMessage("Lista zdjęć nie może być pusta");

        RuleFor(x => x.PublicIds.Count)
            .LessThanOrEqualTo(HardUpperBound)
            .WithMessage($"Maksymalnie {HardUpperBound} zdjęć w jednym żądaniu");

        RuleFor(x => x.ModeratorNote)
            .MaximumLength(500)
            .WithMessage("Uwaga moderatora może mieć maksymalnie 500 znaków");

        RuleFor(x => x)
            .Must(HasAtLeastOneReasonWhenRejecting)
            .WithMessage("Odrzucenie wymaga wybrania co najmniej jednego powodu lub wpisania uwagi moderatora")
            .When(x => !x.Approve);
    }

    private static bool HasAtLeastOneReasonWhenRejecting(BulkModeratePhotosCommand command)
    {
        var hasCodes = command.ReasonCodes is not null
            && command.ReasonCodes.Any(c => !string.IsNullOrWhiteSpace(c));
        var hasNote = !string.IsNullOrWhiteSpace(command.ModeratorNote);
        return hasCodes || hasNote;
    }
}

public class BulkModeratePhotosHandler : IRequestHandler<BulkModeratePhotosCommand, ErrorOr<BulkModerateResult>>
{
    private const string MaxCountConfigKey = "bulk_photo_moderation_max_count";
    private const int DefaultMaxCount = 50;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublicConfigProvider _configProvider;

    public BulkModeratePhotosHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IPublicConfigProvider configProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _configProvider = configProvider;
    }

    public async Task<ErrorOr<BulkModerateResult>> Handle(BulkModeratePhotosCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        if (request.PublicIds is null || request.PublicIds.Count == 0)
            return DomainErrors.Admin.BulkEmpty;

        var maxCount = await _configProvider.GetIntAsync(MaxCountConfigKey, DefaultMaxCount, cancellationToken);
        if (request.PublicIds.Count > maxCount)
            return DomainErrors.Admin.BulkLimitExceeded;

        string? resolvedText = null;
        IReadOnlyList<string> appliedCodes = Array.Empty<string>();

        if (!request.Approve)
        {
            var resolution = await RejectionReasonResolver.ResolveAsync(
                _db, request.ReasonCodes, request.ModeratorNote, RejectionReasonCategory.Photo, cancellationToken);

            if (resolution.IsError)
                return resolution.Errors;

            resolvedText = resolution.Value.ResolvedText;
            appliedCodes = resolution.Value.AppliedCodes;
        }

        var distinctIds = request.PublicIds.Distinct().ToList();

        var assets = await _db.MediaAssets
            .Where(a => distinctIds.Contains(a.PublicId))
            .ToListAsync(cancellationToken);

        var assetsByPublicId = assets.ToDictionary(a => a.PublicId);

        var success = new List<Guid>();
        var failed = new List<BulkModerateFailure>();

        foreach (var publicId in distinctIds)
        {
            if (!assetsByPublicId.TryGetValue(publicId, out var asset))
            {
                failed.Add(new BulkModerateFailure(
                    publicId,
                    DomainErrors.Photo.NotFound.Code,
                    DomainErrors.Photo.NotFound.Description));
                continue;
            }

            if (asset.ModerationStatus != ContentModerationStatus.Pending)
            {
                failed.Add(new BulkModerateFailure(
                    publicId,
                    "PHOTO_ALREADY_MODERATED",
                    "Zdjęcie zostało już zmoderowane"));
                continue;
            }

            await ModeratePhotoLogic.ApplyAsync(
                asset, request.Approve, resolvedText, appliedCodes, _db, _currentUser, cancellationToken);

            success.Add(publicId);
        }

        if (success.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return new BulkModerateResult(success, failed);
    }
}
