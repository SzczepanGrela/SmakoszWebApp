using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateRejectionReason;

public record UpdateRejectionReasonCommand(
    string ReasonCode,
    string Category,
    string AdminLabel,
    string UserMessageTemplate,
    bool IsActive)
    : IRequest<ErrorOr<Success>>;

public class UpdateRejectionReasonValidator : AbstractValidator<UpdateRejectionReasonCommand>
{
    public UpdateRejectionReasonValidator()
    {
        RuleFor(x => x.ReasonCode)
            .NotEmpty().WithMessage("Kod powodu jest wymagany");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Kategoria jest wymagana");

        RuleFor(x => x.AdminLabel)
            .NotEmpty().WithMessage("Etykieta administratora jest wymagana")
            .MinimumLength(3).WithMessage("Etykieta musi mieć co najmniej 3 znaki")
            .MaximumLength(100).WithMessage("Etykieta może mieć maksymalnie 100 znaków");

        RuleFor(x => x.UserMessageTemplate)
            .NotEmpty().WithMessage("Komunikat dla użytkownika jest wymagany")
            .MinimumLength(10).WithMessage("Komunikat musi mieć co najmniej 10 znaków")
            .MaximumLength(500).WithMessage("Komunikat może mieć maksymalnie 500 znaków");
    }
}

public class UpdateRejectionReasonHandler
    : IRequestHandler<UpdateRejectionReasonCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateRejectionReasonHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<Success>> Handle(
        UpdateRejectionReasonCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        if (!Enum.TryParse<RejectionReasonCategory>(request.Category, true, out var category))
            return DomainErrors.RejectionReason.InvalidCategory;

        var entity = await _db.RejectionReasons
            .FirstOrDefaultAsync(r => r.ReasonCode == request.ReasonCode, cancellationToken);

        if (entity is null)
            return DomainErrors.RejectionReason.NotFound;

        if (!string.Equals(entity.AdminLabel, request.AdminLabel, StringComparison.OrdinalIgnoreCase))
        {
            var labelExists = await _db.RejectionReasons
                .AnyAsync(r => r.ReasonCode != request.ReasonCode
                               && r.AdminLabel.ToLower() == request.AdminLabel.ToLower(),
                    cancellationToken);

            if (labelExists)
                return DomainErrors.RejectionReason.LabelAlreadyExists;
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            entity.ReasonCode,
            Category = entity.Category.ToString(),
            entity.AdminLabel,
            entity.UserMessageTemplate,
            entity.IsActive
        });

        entity.Category = category;
        entity.AdminLabel = request.AdminLabel;
        entity.UserMessageTemplate = request.UserMessageTemplate;
        entity.IsActive = request.IsActive;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "RejectionReasons",
            RecordId = 0,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = _dateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new
            {
                entity.ReasonCode,
                Category = entity.Category.ToString(),
                entity.AdminLabel,
                entity.UserMessageTemplate,
                entity.IsActive
            })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
