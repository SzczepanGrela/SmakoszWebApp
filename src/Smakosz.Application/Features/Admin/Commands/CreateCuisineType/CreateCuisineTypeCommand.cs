using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreateCuisineType;

public record CreateCuisineTypeCommand(string Name, string DisplayName, string? Icon, bool IsActive)
    : IRequest<ErrorOr<int>>;

public class CreateCuisineTypeValidator : AbstractValidator<CreateCuisineTypeCommand>
{
    public CreateCuisineTypeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa jest wymagana")
            .MaximumLength(50).WithMessage("Nazwa może mieć maksymalnie 50 znaków");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Nazwa wyświetlana jest wymagana")
            .MaximumLength(100).WithMessage("Nazwa wyświetlana może mieć maksymalnie 100 znaków");

        RuleFor(x => x.Icon)
            .MaximumLength(10).WithMessage("Ikona może mieć maksymalnie 10 znaków");
    }
}

public class CreateCuisineTypeHandler : IRequestHandler<CreateCuisineTypeCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateCuisineTypeHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<int>> Handle(CreateCuisineTypeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var exists = await _db.CuisineTypes
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower(), cancellationToken);

        if (exists)
            return DomainErrors.CuisineType.AlreadyExists;

        var cuisine = new CuisineType
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Icon = request.Icon,
            IsActive = request.IsActive
        };

        _db.CuisineTypes.Add(cuisine);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "CuisineTypes",
            RecordId = cuisine.CuisineTypeId,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = _dateTime.UtcNow,
            NewValues = JsonSerializer.Serialize(new { request.Name, request.DisplayName, request.Icon, request.IsActive })
        });
        await _db.SaveChangesAsync(cancellationToken);

        return cuisine.CuisineTypeId;
    }
}
