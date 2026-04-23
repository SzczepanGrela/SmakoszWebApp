using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreateRejectionReason;

public record CreateRejectionReasonCommand(
    string ReasonCode,
    string Category,
    string AdminLabel,
    string UserMessageTemplate,
    bool IsActive)
    : IRequest<ErrorOr<string>>;

public class CreateRejectionReasonValidator : AbstractValidator<CreateRejectionReasonCommand>
{
    private static readonly Regex ReasonCodePattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public CreateRejectionReasonValidator()
    {
        RuleFor(x => x.ReasonCode)
            .NotEmpty().WithMessage("Kod powodu jest wymagany")
            .MinimumLength(3).WithMessage("Kod musi mieć co najmniej 3 znaki")
            .MaximumLength(50).WithMessage("Kod może mieć maksymalnie 50 znaków")
            .Must(code => ReasonCodePattern.IsMatch(code))
            .WithMessage("Kod może zawierać tylko małe litery, cyfry i podkreślenia, zaczynając od litery");

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

public class CreateRejectionReasonHandler
    : IRequestHandler<CreateRejectionReasonCommand, ErrorOr<string>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateRejectionReasonHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<string>> Handle(
        CreateRejectionReasonCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        if (!Enum.TryParse<RejectionReasonCategory>(request.Category, true, out var category))
            return DomainErrors.RejectionReason.InvalidCategory;

        var normalizedCode = request.ReasonCode.ToLower();

        var codeExists = await _db.RejectionReasons
            .AnyAsync(r => r.ReasonCode == normalizedCode, cancellationToken);

        if (codeExists)
            return DomainErrors.RejectionReason.CodeAlreadyExists;

        var labelExists = await _db.RejectionReasons
            .AnyAsync(r => r.AdminLabel.ToLower() == request.AdminLabel.ToLower(), cancellationToken);

        if (labelExists)
            return DomainErrors.RejectionReason.LabelAlreadyExists;

        var entity = new RejectionReason
        {
            ReasonCode = normalizedCode,
            Category = category,
            AdminLabel = request.AdminLabel,
            UserMessageTemplate = request.UserMessageTemplate,
            IsActive = request.IsActive,
            CreatedAt = _dateTime.UtcNow
        };

        _db.RejectionReasons.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "RejectionReasons",
            RecordId = 0,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = _dateTime.UtcNow,
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

        return entity.ReasonCode;
    }
}
