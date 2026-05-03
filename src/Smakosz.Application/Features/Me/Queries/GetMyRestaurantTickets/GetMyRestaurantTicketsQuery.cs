using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Queries.GetMyRestaurantTickets;

public record GetMyRestaurantTicketsQuery() : IRequest<ErrorOr<List<MyRestaurantTicketDto>>>;

public class MyRestaurantTicketDto
{
    public int TicketId { get; set; }
    public string TicketType { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
    public string? Resolution { get; set; }
}

public class GetMyRestaurantTicketsHandler : IRequestHandler<GetMyRestaurantTicketsQuery, ErrorOr<List<MyRestaurantTicketDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyRestaurantTicketsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<MyRestaurantTicketDto>>> Handle(GetMyRestaurantTicketsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var rows = await _db.SystemTickets
            .AsNoTracking()
            .Where(t => t.RequesterId == userId
                && (t.TicketType == TicketType.RestaurantClaim || t.TicketType == TicketType.RestaurantRequest))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new MyRestaurantTicketDto
            {
                TicketId = t.TicketId,
                TicketType = t.TicketType.ToString(),
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt,
                Resolution = t.Resolution
            })
            .ToListAsync(cancellationToken);

        return rows;
    }
}
