using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ModerateReview;

public record ModerateReviewCommand(Guid PublicId, bool Approve, string? RejectionReason) : IRequest<ErrorOr<Success>>;

public class ModerateReviewHandler : IRequestHandler<ModerateReviewCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ModerateReviewHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ModerateReviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PublicId == request.PublicId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return DomainErrors.Review.NotFound;

        review.ContentStatus = request.Approve ? ReviewContentStatus.Approved : ReviewContentStatus.Rejected;

        if (!request.Approve)
            review.ContentRejectionReason = request.RejectionReason;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
