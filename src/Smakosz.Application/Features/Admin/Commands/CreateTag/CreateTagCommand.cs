using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreateTag;

public record CreateTagCommand(string Name, string Category, string TargetEntity, string? DisplayColor)
    : IRequest<ErrorOr<int>>;

public class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa tagu jest wymagana")
            .MaximumLength(50).WithMessage("Nazwa tagu może mieć maksymalnie 50 znaków");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Kategoria jest wymagana")
            .MaximumLength(30).WithMessage("Kategoria może mieć maksymalnie 30 znaków");
    }
}

public class CreateTagHandler : IRequestHandler<CreateTagCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateTagHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<int>> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var exists = await _db.Tags
            .AnyAsync(t => t.TagName.ToLower() == request.Name.ToLower(), cancellationToken);

        if (exists)
            return DomainErrors.Tag.AlreadyExists;

        if (!Enum.TryParse<TagTargetEntity>(request.TargetEntity, true, out var targetEntity))
            targetEntity = TagTargetEntity.Both;

        var tag = new Tag
        {
            TagName = request.Name,
            Category = request.Category,
            TargetEntity = targetEntity,
            DisplayColor = request.DisplayColor,
            CreatedAt = _dateTime.UtcNow
        };

        _db.Tags.Add(tag);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Tags",
            RecordId = tag.TagId,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = _dateTime.UtcNow,
            NewValues = JsonSerializer.Serialize(new { request.Name, request.Category, request.TargetEntity, request.DisplayColor })
        });
        await _db.SaveChangesAsync(cancellationToken);

        return tag.TagId;
    }
}
