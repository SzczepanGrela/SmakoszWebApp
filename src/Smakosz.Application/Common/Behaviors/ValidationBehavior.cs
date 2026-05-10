using FluentValidation;
using MediatR;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly IBusinessMetrics _metrics;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators, IBusinessMetrics metrics)
    {
        _validators = validators;
        _metrics = metrics;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            _metrics.RecordValidationFailure(typeof(TRequest).Name);
            throw new ValidationException(failures);
        }

        return await next();
    }
}
