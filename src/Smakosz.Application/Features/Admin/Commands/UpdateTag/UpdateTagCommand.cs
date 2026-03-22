using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateTag;

public record UpdateTagCommand(int TagId, string? Name, string? Category, string? TargetEntity, string? DisplayColor)
    : IRequest<ErrorOr<Success>>;

public class UpdateTagHandler : IRequestHandler<UpdateTagCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateTagHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var tag = await _db.Tags
            .FirstOrDefaultAsync(t => t.TagId == request.TagId, cancellationToken);

        if (tag is null)
            return DomainErrors.Tag.NotFound;

        if (request.Name is not null && request.Name.ToLower() != tag.TagName.ToLower())
        {
            var duplicate = await _db.Tags
                .AnyAsync(t => t.TagName.ToLower() == request.Name.ToLower() && t.TagId != request.TagId, cancellationToken);

            if (duplicate)
                return DomainErrors.Tag.AlreadyExists;
        }

        var oldValues = JsonSerializer.Serialize(new { tag.TagName, tag.Category, TargetEntity = tag.TargetEntity.ToString(), tag.DisplayColor });

        if (request.Name is not null)
            tag.TagName = request.Name;

        if (request.Category is not null)
            tag.Category = request.Category;

        if (request.TargetEntity is not null && Enum.TryParse<TagTargetEntity>(request.TargetEntity, true, out var targetEntity))
            tag.TargetEntity = targetEntity;

        if (request.DisplayColor is not null)
            tag.DisplayColor = request.DisplayColor;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Tags",
            RecordId = tag.TagId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { tag.TagName, tag.Category, TargetEntity = tag.TargetEntity.ToString(), tag.DisplayColor })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
