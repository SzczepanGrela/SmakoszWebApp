using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteIngredient;

public record DeleteIngredientCommand(int IngredientId) : IRequest<ErrorOr<Success>>;

public class DeleteIngredientHandler : IRequestHandler<DeleteIngredientCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteIngredientHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteIngredientCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var ingredient = await _db.Ingredients
            .FirstOrDefaultAsync(i => i.IngredientId == request.IngredientId, cancellationToken);

        if (ingredient is null)
            return DomainErrors.Ingredient.NotFound;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Ingredients",
            RecordId = ingredient.IngredientId,
            Operation = AuditOperation.Delete,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(new { ingredient.IngredientName })
        });

        _db.Ingredients.Remove(ingredient);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
