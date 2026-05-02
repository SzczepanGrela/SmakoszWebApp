using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ModeratePhoto;

public record ModeratePhotoCommand(
    Guid PublicId,
    bool Approve,
    IReadOnlyList<string>? ReasonCodes,
    string? ModeratorNote) : IRequest<ErrorOr<Success>>;

public class ModeratePhotoValidator : AbstractValidator<ModeratePhotoCommand>
{
    public ModeratePhotoValidator()
    {
        RuleFor(x => x.ModeratorNote)
            .MaximumLength(500)
            .WithMessage("Uwaga moderatora może mieć maksymalnie 500 znaków");

        RuleFor(x => x)
            .Must(HasAtLeastOneReasonWhenRejecting)
            .WithMessage("Odrzucenie wymaga wybrania co najmniej jednego powodu lub wpisania uwagi moderatora")
            .When(x => !x.Approve);
    }

    private static bool HasAtLeastOneReasonWhenRejecting(ModeratePhotoCommand command)
    {
        var hasCodes = command.ReasonCodes is not null
            && command.ReasonCodes.Any(c => !string.IsNullOrWhiteSpace(c));
        var hasNote = !string.IsNullOrWhiteSpace(command.ModeratorNote);
        return hasCodes || hasNote;
    }
}

public class ModeratePhotoHandler : IRequestHandler<ModeratePhotoCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ModeratePhotoHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ModeratePhotoCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var asset = await _db.MediaAssets
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId, cancellationToken);

        if (asset is null)
            return DomainErrors.Photo.NotFound;

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

        await ModeratePhotoLogic.ApplyAsync(
            asset, request.Approve, resolvedText, appliedCodes, _db, _currentUser, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
