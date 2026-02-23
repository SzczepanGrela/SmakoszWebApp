using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Reviews.Commands.DeleteReview;

public class DeleteReviewHandler : IRequestHandler<DeleteReviewCommand, ErrorOr<Deleted>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteReviewHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Deleted>> Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PublicId == request.ReviewPublicId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return DomainErrors.Review.NotFound;

        if (review.UserId != _currentUser.UserId.Value)
            return DomainErrors.Review.NotOwner;

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
