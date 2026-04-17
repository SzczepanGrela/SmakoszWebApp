using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteRejectionReason;

public record DeleteRejectionReasonCommand(string ReasonCode) : IRequest<ErrorOr<Success>>;

public class DeleteRejectionReasonValidator : AbstractValidator<DeleteRejectionReasonCommand>
{
    public DeleteRejectionReasonValidator()
    {
        RuleFor(x => x.ReasonCode)
            .NotEmpty().WithMessage("Kod powodu jest wymagany");
    }
}

public class DeleteRejectionReasonHandler
    : IRequestHandler<DeleteRejectionReasonCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteRejectionReasonHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<Success>> Handle(
        DeleteRejectionReasonCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var entity = await _db.RejectionReasons
            .FirstOrDefaultAsync(r => r.ReasonCode == request.ReasonCode, cancellationToken);

        if (entity is null)
            return DomainErrors.RejectionReason.NotFound;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "RejectionReasons",
            RecordId = 0,
            Operation = AuditOperation.Delete,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = _dateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(new
            {
                entity.ReasonCode,
                Category = entity.Category.ToString(),
                entity.AdminLabel,
                entity.UserMessageTemplate,
                entity.IsActive
            })
        });

        _db.RejectionReasons.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
