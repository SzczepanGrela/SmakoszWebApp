using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.AdminCreateRestaurant;

public record AdminCreateRestaurantCommand(
    string Name,
    string Address,
    int CityId,
    int CuisineTypeId,
    string? Phone,
    string? Email,
    string? Description,
    int? OwnerId,
    int? TicketId) : IRequest<ErrorOr<int>>;

public class AdminCreateRestaurantHandler : IRequestHandler<AdminCreateRestaurantCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public AdminCreateRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<int>> Handle(AdminCreateRestaurantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin || !_currentUser.UserId.HasValue)
            return DomainErrors.Admin.Forbidden;

        if (await _forbiddenWords.ContainsAsync(request.Name, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;
        if (request.Description is not null && await _forbiddenWords.ContainsAsync(request.Description, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;

        User? owner = null;
        if (request.OwnerId.HasValue)
        {
            owner = await _db.Users
                .FirstOrDefaultAsync(u => u.UserId == request.OwnerId.Value && !u.IsDeleted, cancellationToken);
            if (owner is null)
                return DomainErrors.User.NotFound;

            var alreadyOwns = await _db.Restaurants
                .AnyAsync(r => r.OwnerId == request.OwnerId.Value, cancellationToken);
            if (alreadyOwns)
                return DomainErrors.Business.UserAlreadyOwnsRestaurant;
        }

        SystemTicket? ticket = null;
        if (request.TicketId.HasValue)
        {
            ticket = await _db.SystemTickets
                .FirstOrDefaultAsync(t => t.TicketId == request.TicketId.Value, cancellationToken);
            if (ticket is null)
                return DomainErrors.Ticket.NotFound;
            if (ticket.TicketType != TicketType.RestaurantRequest)
                return DomainErrors.Ticket.WrongType;
            if (ticket.Status != TicketStatus.Open)
                return DomainErrors.Ticket.NotPending;
            if (request.OwnerId.HasValue && ticket.RequesterId != request.OwnerId.Value)
                return DomainErrors.Ticket.RequesterMismatch;
        }

        var now = DateTime.UtcNow;
        var restaurant = new Restaurant
        {
            PublicId = Guid.NewGuid(),
            OwnerId = request.OwnerId,
            RestaurantName = request.Name,
            Address = request.Address,
            CityId = request.CityId,
            CuisineTypeId = request.CuisineTypeId,
            Phone = request.Phone,
            Email = request.Email,
            Description = request.Description,
            Status = RestaurantStatus.Active,
            IsVerified = true,
            VerifiedAt = now,
            VerifiedBy = _currentUser.UserId,
            ModerationStatus = ContentModerationStatus.Approved,
            CreatedAt = now
        };

        _db.Restaurants.Add(restaurant);

        if (owner is not null)
        {
            owner.Role = UserRole.Restaurant;
            owner.UpdatedAt = now;
        }

        if (ticket is not null)
        {
            ticket.Status = TicketStatus.Resolved;
            ticket.ResolvedAt = now;
            ticket.ResolvedByAdminId = _currentUser.UserId;
            ticket.Resolution = $"Restaurant created (name={restaurant.RestaurantName})";
            ticket.UpdatedAt = now;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "restaurants",
            RecordId = 0,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = now,
            NewValues = JsonSerializer.Serialize(new
            {
                Name = request.Name,
                OwnerId = request.OwnerId,
                TicketId = request.TicketId,
                CityId = request.CityId,
                CuisineTypeId = request.CuisineTypeId
            })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return restaurant.RestaurantId;
    }
}

public class AdminCreateRestaurantValidator : AbstractValidator<AdminCreateRestaurantCommand>
{
    public AdminCreateRestaurantValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa restauracji jest wymagana")
            .MinimumLength(2).WithMessage("Nazwa restauracji musi mieć co najmniej 2 znaki")
            .MaximumLength(200).WithMessage("Nazwa restauracji może mieć maksymalnie 200 znaków");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Adres jest wymagany")
            .MinimumLength(5).WithMessage("Adres musi mieć co najmniej 5 znaków")
            .MaximumLength(300).WithMessage("Adres może mieć maksymalnie 300 znaków");

        RuleFor(x => x.CityId)
            .GreaterThan(0).WithMessage("Miasto jest wymagane");

        RuleFor(x => x.CuisineTypeId)
            .GreaterThan(0).WithMessage("Typ kuchni jest wymagany");

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email!)
                .EmailAddress().WithMessage("Podany adres email jest nieprawidłowy");
        });

        When(x => x.Phone is not null, () =>
        {
            RuleFor(x => x.Phone!)
                .Matches(@"^[\d\s\+\-\(\)]+$").WithMessage("Numer telefonu zawiera niedozwolone znaki");
        });
    }
}
