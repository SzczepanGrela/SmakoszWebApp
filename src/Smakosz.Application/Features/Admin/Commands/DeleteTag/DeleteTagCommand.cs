using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteTag;

public record DeleteTagCommand(int TagId) : IRequest<ErrorOr<Success>>;

public class DeleteTagValidator : AbstractValidator<DeleteTagCommand>
{
    public DeleteTagValidator()
    {
        RuleFor(x => x.TagId)
            .GreaterThan(0).WithMessage("Nieprawidłowe ID tagu");
    }
}

public class DeleteTagHandler : IRequestHandler<DeleteTagCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteTagHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var tag = await _db.Tags
            .FirstOrDefaultAsync(t => t.TagId == request.TagId, cancellationToken);

        if (tag is null)
            return DomainErrors.Tag.NotFound;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Tags",
            RecordId = tag.TagId,
            Operation = AuditOperation.Delete,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(new { tag.TagName, tag.Category, TargetEntity = tag.TargetEntity.ToString(), tag.DisplayColor })
        });

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
