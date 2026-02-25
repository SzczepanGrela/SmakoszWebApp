using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Admin.Commands.CreateCity;

public record CreateCityCommand(string Name, string? Region) : IRequest<ErrorOr<int>>;

public class CreateCityValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa miasta jest wymagana")
            .MaximumLength(100).WithMessage("Nazwa miasta może mieć maksymalnie 100 znaków");
    }
}

public class CreateCityHandler : IRequestHandler<CreateCityCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateCityHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<int>> Handle(CreateCityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var exists = await _db.Cities
            .AnyAsync(c => c.CityName.ToLower() == request.Name.ToLower(), cancellationToken);

        if (exists)
            return DomainErrors.City.AlreadyExists;

        var city = new City
        {
            CityName = request.Name,
            Region = request.Region,
            CreatedAt = _dateTime.UtcNow
        };

        _db.Cities.Add(city);
        await _db.SaveChangesAsync(cancellationToken);

        return city.CityId;
    }
}
