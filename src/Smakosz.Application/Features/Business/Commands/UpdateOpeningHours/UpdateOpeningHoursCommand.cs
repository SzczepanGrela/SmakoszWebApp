using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Business.Commands.UpdateOpeningHours;

public record UpdateOpeningHoursCommand(List<OpeningHoursItemDto> Hours) : IRequest<ErrorOr<Success>>;

public class UpdateOpeningHoursHandler : IRequestHandler<UpdateOpeningHoursCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateOpeningHoursHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateOpeningHoursCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var existing = await _db.RestaurantOpeningHours
            .Where(oh => oh.RestaurantId == restaurant.RestaurantId)
            .ToListAsync(cancellationToken);

        _db.RestaurantOpeningHours.RemoveRange(existing);

        foreach (var item in request.Hours)
        {
            _db.RestaurantOpeningHours.Add(new RestaurantOpeningHours
            {
                RestaurantId = restaurant.RestaurantId,
                DayOfWeek = item.DayOfWeek,
                OpenTime = TimeOnly.TryParse(item.OpenTime, out var openTime) ? openTime : TimeOnly.MinValue,
                CloseTime = TimeOnly.TryParse(item.CloseTime, out var closeTime) ? closeTime : TimeOnly.MinValue,
                IsClosed = item.IsClosed
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}

public class UpdateOpeningHoursValidator : AbstractValidator<UpdateOpeningHoursCommand>
{
    public UpdateOpeningHoursValidator()
    {
        RuleFor(x => x.Hours).NotNull();
        RuleForEach(x => x.Hours).ChildRules(item =>
        {
            item.RuleFor(h => h.DayOfWeek)
                .InclusiveBetween(0, 6).WithMessage("DayOfWeek musi być w zakresie 0-6");
        });
    }
}
