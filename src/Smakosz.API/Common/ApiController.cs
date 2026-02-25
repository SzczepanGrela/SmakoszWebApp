using ErrorOr;

namespace Smakosz.API.Common;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected IActionResult ToActionResult<T>(ErrorOr<T> result)
    {
        if (!result.IsError)
        {
            return Ok(new ApiResponse<T>
            {
                Success = true,
                Data = result.Value
            });
        }

        return ToErrorResult(result.FirstError);
    }

    protected IActionResult ToCreatedResult<T>(ErrorOr<T> result, string? location = null)
    {
        if (!result.IsError)
        {
            var response = new ApiResponse<T>
            {
                Success = true,
                Data = result.Value
            };

            if (location is not null)
                return Created(location, response);

            return StatusCode(StatusCodes.Status201Created, response);
        }

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

        return StatusCode(statusCode, new ApiResponse<object>
        {
            Success = false,
            Error = new ApiError
            {
                Code = error.Code,
                Message = error.Description
            }
        });
    }
}
