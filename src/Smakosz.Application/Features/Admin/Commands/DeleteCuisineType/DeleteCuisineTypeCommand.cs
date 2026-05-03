using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteCuisineType;

public record DeleteCuisineTypeCommand(int CuisineTypeId) : IRequest<ErrorOr<Success>>;

public class DeleteCuisineTypeValidator : AbstractValidator<DeleteCuisineTypeCommand>
{
    public DeleteCuisineTypeValidator()
    {
        RuleFor(x => x.CuisineTypeId)
            .GreaterThan(0).WithMessage("Nieprawidłowe ID kuchni");
    }
}

public class DeleteCuisineTypeHandler : IRequestHandler<DeleteCuisineTypeCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteCuisineTypeHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteCuisineTypeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var cuisine = await _db.CuisineTypes
            .FirstOrDefaultAsync(c => c.CuisineTypeId == request.CuisineTypeId, cancellationToken);

        if (cuisine is null)
            return DomainErrors.CuisineType.NotFound;

        var hasRestaurants = await _db.Restaurants
            .AnyAsync(r => r.CuisineTypeId == request.CuisineTypeId, cancellationToken);

        if (hasRestaurants)
            return DomainErrors.CuisineType.HasRestaurants;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "CuisineTypes",
            RecordId = cuisine.CuisineTypeId,
            Operation = AuditOperation.Delete,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(new { cuisine.Name, cuisine.DisplayName, cuisine.Icon, cuisine.IsActive })
        });

        _db.CuisineTypes.Remove(cuisine);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
