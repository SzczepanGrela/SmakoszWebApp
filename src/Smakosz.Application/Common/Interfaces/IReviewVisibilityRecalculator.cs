namespace Smakosz.Application.Common.Interfaces;

public interface IReviewVisibilityRecalculator
{
    Task EvaluateAsync(int reviewId, CancellationToken ct);
}
