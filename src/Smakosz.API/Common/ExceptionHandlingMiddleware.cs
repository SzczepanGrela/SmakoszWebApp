using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Smakosz.API.Common;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error");

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "VALIDATION_ERROR",
                    Message = "Walidacja nie powiodła się",
                    Details = errors
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict detected");

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "CONCURRENCY_CONFLICT",
                    Message = "Obiekt został zmodyfikowany przez innego użytkownika. Odśwież dane i spróbuj ponownie."
                }
            });
        }
        catch (Exception ex) when (IsTransientDatabaseFailure(ex))
        {
            _logger.LogWarning(ex, "Database temporarily unavailable (pool or server connection limit reached)");

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "5";
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "DATABASE_UNAVAILABLE",
                    Message = "Serwer jest tymczasowo przeciążony. Spróbuj ponownie za chwilę."
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = _env.IsProduction()
                        ? "Wystąpił nieoczekiwany błąd serwera"
                        : $"{ex.GetType().Name}: {ex.Message}"
                }
            });
        }
    }

    private static bool IsTransientDatabaseFailure(Exception ex)
    {
        var inner = ex;
        while (inner is not null)
        {
            if (inner is NpgsqlException npg && npg.IsTransient)
                return true;
            inner = inner.InnerException;
        }
        return false;
    }
}
