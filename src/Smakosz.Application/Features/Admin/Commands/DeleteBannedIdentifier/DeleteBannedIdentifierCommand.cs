using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteBannedIdentifier;

public record DeleteBannedIdentifierCommand(int BanId) : IRequest<ErrorOr<Success>>;

public class DeleteBannedIdentifierValidator : AbstractValidator<DeleteBannedIdentifierCommand>
{
    public DeleteBannedIdentifierValidator()
    {
        RuleFor(x => x.BanId).GreaterThan(0).WithMessage("Nieprawidłowe ID");
    }
}

public class DeleteBannedIdentifierHandler : IRequestHandler<DeleteBannedIdentifierCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteBannedIdentifierHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteBannedIdentifierCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var ban = await _db.BannedIdentifiers
            .FirstOrDefaultAsync(b => b.BanId == request.BanId, cancellationToken);

        if (ban is null)
            return DomainErrors.BannedIdentifier.NotFound;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "banned_identifiers",
            RecordId = ban.BanId,
            Operation = AuditOperation.Delete,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(new { Type = ban.Type.ToString(), ban.Value, ban.Reason, ban.ExpiresAt })
        });

        _db.BannedIdentifiers.Remove(ban);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
