using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Business.Commands.RegisterBusiness;

public record RegisterBusinessCommand(
    string Name,
    string? Description,
    string? Address,
    string? Phone,
    string? Email,
    int? CityId) : IRequest<ErrorOr<int>>;

public class RegisterBusinessHandler : IRequestHandler<RegisterBusinessCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RegisterBusinessHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<int>> Handle(RegisterBusinessCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var existingRestaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (existingRestaurant is not null)
        {
            if (existingRestaurant.Status == RestaurantStatus.PendingVerification)
                return DomainErrors.Business.RegistrationPending;

            return DomainErrors.Business.RestaurantExists;
        }

        var restaurant = new Restaurant
        {
            PublicId = Guid.NewGuid(),
            OwnerId = _currentUser.UserId.Value,
            RestaurantName = request.Name,
            Description = request.Description,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            CityId = request.CityId,
            Status = RestaurantStatus.PendingVerification,
            CreatedAt = DateTime.UtcNow
        };

        _db.Restaurants.Add(restaurant);
        await _db.SaveChangesAsync(cancellationToken);

        return restaurant.RestaurantId;
    }
}

public class RegisterBusinessValidator : AbstractValidator<RegisterBusinessCommand>
{
    public RegisterBusinessValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa restauracji jest wymagana")
            .MinimumLength(2).WithMessage("Nazwa restauracji musi mieć co najmniej 2 znaki")
            .MaximumLength(200).WithMessage("Nazwa restauracji może mieć maksymalnie 200 znaków");

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email!)
                .EmailAddress().WithMessage("Podany adres email jest nieprawidłowy");
        });
    }
}
