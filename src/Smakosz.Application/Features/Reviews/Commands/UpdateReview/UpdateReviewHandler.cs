using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reviews.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Reviews.Commands.UpdateReview;

public class UpdateReviewHandler : IRequestHandler<UpdateReviewCommand, ErrorOr<ReviewCardDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateReviewHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<ReviewCardDto>> Handle(UpdateReviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var review = await _db.Reviews
            .Include(r => r.User)
            .Include(r => r.Dish)
            .Include(r => r.Restaurant)
            .FirstOrDefaultAsync(r => r.PublicId == request.ReviewPublicId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return DomainErrors.Review.NotFound;

        if (review.UserId != _currentUser.UserId.Value)
            return DomainErrors.Review.NotOwner;

        review.DishRating = request.DishRating;
        review.ServiceRating = request.ServiceRating;
        review.CleanlinessRating = request.CleanlinessRating;
        review.AmbianceRating = request.AmbianceRating;
        review.Content = request.Content;
        review.VisitDate = request.VisitDate;
        review.ContentStatus = string.IsNullOrEmpty(request.Content) ? ReviewContentStatus.None : ReviewContentStatus.Pending;

        await _db.SaveChangesAsync(cancellationToken);

        return review.ToCardDto(false);
    }
}
