using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace Smakosz.Orchestrator.Common;

[ApiController]
public abstract class OrchestratorController : ControllerBase
{
    protected IActionResult ToActionResult<T>(ErrorOr<T> result)
    {
        if (!result.IsError)
            return Ok(result.Value);

        return ToErrorResult(result.FirstError);
    }

    protected IActionResult ToNoContentResult(ErrorOr<Deleted> result)
    {
        if (!result.IsError)
            return NoContent();

        return ToErrorResult(result.FirstError);
    }

    protected IActionResult ToNoContentResult(ErrorOr<Success> result)
    {
        if (!result.IsError)
            return NoContent();

        return ToErrorResult(result.FirstError);
    }

    private IActionResult ToErrorResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { error = new { code = error.Code, message = error.Description } });
    }
}
