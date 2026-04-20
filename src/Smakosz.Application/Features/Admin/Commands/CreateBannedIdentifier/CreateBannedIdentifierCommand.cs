using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreateBannedIdentifier;

public record CreateBannedIdentifierCommand(
    BannedIdentifierType Type,
    string Value,
    string? Reason,
    DateTime? ExpiresAt) : IRequest<ErrorOr<int>>;

public class CreateBannedIdentifierValidator : AbstractValidator<CreateBannedIdentifierCommand>
{
    public CreateBannedIdentifierValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Wartość jest wymagana")
            .MaximumLength(255).WithMessage("Wartość może mieć maksymalnie 255 znaków");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Powód moze miec maksymalnie 500 znakow")
            .When(x => x.Reason is not null);
    }
}

public class CreateBannedIdentifierHandler : IRequestHandler<CreateBannedIdentifierCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateBannedIdentifierHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<int>> Handle(CreateBannedIdentifierCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var exists = await _db.BannedIdentifiers
            .AnyAsync(b => b.Type == request.Type && b.Value.ToLower() == request.Value.ToLower(), cancellationToken);

        if (exists)
            return DomainErrors.BannedIdentifier.AlreadyExists;

        var ban = new BannedIdentifier
        {
            Type = request.Type,
            Value = request.Value,
            Reason = request.Reason,
            BannedBy = _currentUser.UserId,
            BannedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt
        };

        _db.BannedIdentifiers.Add(ban);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "banned_identifiers",
            RecordId = ban.BanId,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            NewValues = JsonSerializer.Serialize(new { Type = request.Type.ToString(), request.Value, request.Reason, request.ExpiresAt })
        });
        await _db.SaveChangesAsync(cancellationToken);

        return ban.BanId;
    }
}
