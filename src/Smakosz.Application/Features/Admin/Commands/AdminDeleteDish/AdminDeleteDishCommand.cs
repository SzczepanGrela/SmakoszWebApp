using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.AdminDeleteDish;

public record AdminDeleteDishCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class AdminDeleteDishHandler : IRequestHandler<AdminDeleteDishCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AdminDeleteDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(AdminDeleteDishCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var dish = await _db.Dishes
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var snapshot = JsonSerializer.Serialize(new
        {
            dish.DishId,
            dish.DishName,
            dish.RestaurantId,
            dish.Price,
            dish.IsAvailable
        });

        _db.Dishes.Remove(dish);

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "dishes",
            RecordId = dish.DishId,
            Operation = AuditOperation.Delete,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = snapshot,
            NewValues = null
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
