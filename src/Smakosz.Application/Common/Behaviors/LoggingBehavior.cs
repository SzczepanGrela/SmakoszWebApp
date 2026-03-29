using System.Diagnostics;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Smakosz.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > 500)
        {
            _logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMs}ms)",
                requestName, stopwatch.ElapsedMilliseconds);
        }

        if (response is IErrorOr { IsError: true, Errors: { Count: > 0 } errors })
        {
            var first = errors[0];
            var level = first.Type is ErrorType.Unauthorized or ErrorType.Forbidden
                ? LogLevel.Warning
                : LogLevel.Information;

            _logger.Log(level,
                "Request {RequestName} failed: {ErrorCode} - {ErrorMessage}",
                requestName, first.Code, first.Description);
        }

        return response;
    }
}
