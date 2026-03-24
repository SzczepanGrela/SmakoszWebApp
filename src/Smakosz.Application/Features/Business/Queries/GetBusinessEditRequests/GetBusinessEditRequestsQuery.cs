using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessEditRequests;

public record GetBusinessEditRequestsQuery : IRequest<ErrorOr<List<BusinessEditRequestDto>>>;

public class BusinessEditRequestDto
{
    public int RequestId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class GetBusinessEditRequestsHandler : IRequestHandler<GetBusinessEditRequestsQuery, ErrorOr<List<BusinessEditRequestDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessEditRequestsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<BusinessEditRequestDto>>> Handle(GetBusinessEditRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var editRequests = await _db.RestaurantEditRequests
            .AsNoTracking()
            .Where(er => er.RestaurantId == restaurant.RestaurantId)
            .OrderByDescending(er => er.CreatedAt)
            .Select(er => new BusinessEditRequestDto
            {
                RequestId = er.RequestId,
                ChangeType = er.ChangeType.ToString(),
                Status = er.Status.ToString(),
                CreatedAt = er.CreatedAt,
                ResolvedAt = er.ResolvedAt,
                RejectionReason = er.RejectionReason
            })
            .ToListAsync(cancellationToken);

        return editRequests;
    }
}
