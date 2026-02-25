using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

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

        _db.Ingredients.Remove(ingredient);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
