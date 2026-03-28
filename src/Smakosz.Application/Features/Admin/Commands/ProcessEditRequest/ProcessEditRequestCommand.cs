using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ProcessEditRequest;

public record ProcessEditRequestCommand(int RequestId, bool Approve, string? RejectionReason) : IRequest<ErrorOr<Success>>;

public class ProcessEditRequestHandler : IRequestHandler<ProcessEditRequestCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public ProcessEditRequestHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<Success>> Handle(ProcessEditRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var editRequest = await _db.RestaurantEditRequests
            .FirstOrDefaultAsync(r => r.RequestId == request.RequestId, cancellationToken);

        if (editRequest is null)
            return DomainErrors.EditRequest.NotFound;

        editRequest.Status = request.Approve ? EditRequestStatus.Approved : EditRequestStatus.Rejected;
        editRequest.ResolvedAt = _dateTime.UtcNow;
        editRequest.ResolvedByAdminId = _currentUser.UserId;
        editRequest.ReviewedBy = _currentUser.UserId;
        editRequest.ReviewedAt = _dateTime.UtcNow;

        if (!request.Approve)
            editRequest.RejectionReason = request.RejectionReason;

        var relatedTicket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketType == TicketType.EditRequest
                && t.ReferenceId == editRequest.RequestId
                && t.Status != TicketStatus.Resolved
                && t.Status != TicketStatus.Closed, cancellationToken);
        if (relatedTicket != null)
        {
            relatedTicket.Status = TicketStatus.Resolved;
            relatedTicket.AssignedAdminId = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
