using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateBannedIdentifier;

public record UpdateBannedIdentifierCommand(
    int BanId,
    string? Reason,
    DateTime? ExpiresAt,
    bool ClearExpiration = false) : IRequest<ErrorOr<Success>>;

public class UpdateBannedIdentifierHandler : IRequestHandler<UpdateBannedIdentifierCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateBannedIdentifierHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateBannedIdentifierCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var ban = await _db.BannedIdentifiers
            .FirstOrDefaultAsync(b => b.BanId == request.BanId, cancellationToken);

        if (ban is null)
            return DomainErrors.BannedIdentifier.NotFound;

        var oldValues = JsonSerializer.Serialize(new { ban.Reason, ban.ExpiresAt });

        if (request.Reason is not null) ban.Reason = request.Reason;

        if (request.ClearExpiration)
            ban.ExpiresAt = null;
        else if (request.ExpiresAt.HasValue)
            ban.ExpiresAt = request.ExpiresAt.Value;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "banned_identifiers",
            RecordId = ban.BanId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { ban.Reason, ban.ExpiresAt })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
