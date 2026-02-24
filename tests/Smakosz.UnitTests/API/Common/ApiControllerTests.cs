using ErrorOr;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smakosz.API.Common;

namespace Smakosz.UnitTests.API.Common;

[Trait("Category", "Controllers")]
public class ApiControllerTests
{
    private readonly TestableApiController _sut;

    public ApiControllerTests()
    {
        _sut = new TestableApiController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void ToActionResult_Success_Returns200WithEnvelope()
    {
        ErrorOr<string> result = "test data";

        var actionResult = _sut.TestToActionResult(result);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var envelope = okResult.Value.Should().BeOfType<ApiResponse<string>>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Data.Should().Be("test data");
    }

    [Fact]
    public void ToActionResult_NotFoundError_Returns404()
    {
        ErrorOr<string> result = Error.NotFound("Entity.NotFound", "Entity was not found");

        var actionResult = _sut.TestToActionResult(result);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);

        var envelope = objectResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("Entity.NotFound");
    }

    [Fact]
    public void ToActionResult_ConflictError_Returns409()
    {
        ErrorOr<string> result = Error.Conflict("Entity.Conflict", "Entity already exists");

        var actionResult = _sut.TestToActionResult(result);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public void ToActionResult_ValidationError_Returns422()
    {
        ErrorOr<string> result = Error.Validation("Field.Invalid", "Field is invalid");

        var actionResult = _sut.TestToActionResult(result);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
    }

    [Fact]
    public void ToActionResult_UnauthorizedError_Returns401()
    {
        ErrorOr<string> result = Error.Unauthorized("Auth.Unauthorized", "Not authorized");

        var actionResult = _sut.TestToActionResult(result);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public void ToActionResult_ForbiddenError_Returns403()
    {
        ErrorOr<string> result = Error.Forbidden("Auth.Forbidden", "Access denied");

        var actionResult = _sut.TestToActionResult(result);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public void ToCreatedResult_Success_Returns201()
    {
        ErrorOr<string> result = "created data";

        var actionResult = _sut.TestToCreatedResult(result);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);

        var envelope = objectResult.Value.Should().BeOfType<ApiResponse<string>>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Data.Should().Be("created data");
    }

    [Fact]
    public void ToCreatedResult_WithLocation_ReturnsCreatedWithLocation()
    {
        ErrorOr<string> result = "created data";

        var actionResult = _sut.TestToCreatedResult(result, "/api/items/1");

        var createdResult = actionResult.Should().BeOfType<CreatedResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Location.Should().Be("/api/items/1");
    }

    [Fact]
    public void ToNoContentResult_Success_Returns204()
    {
        ErrorOr<Deleted> result = Result.Deleted;

        var actionResult = _sut.TestToNoContentResult(result);

        var noContentResult = actionResult.Should().BeOfType<NoContentResult>().Subject;
        noContentResult.StatusCode.Should().Be(204);
    }

    private class TestableApiController : ApiController
    {
        public IActionResult TestToActionResult<T>(ErrorOr<T> result)
            => ToActionResult(result);

        public IActionResult TestToCreatedResult<T>(ErrorOr<T> result, string? location = null)
            => ToCreatedResult(result, location);

        public IActionResult TestToNoContentResult(ErrorOr<Deleted> result)
            => ToNoContentResult(result);
    }
}
