using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.EdgeCases;

public class ValidationTests : IntegrationTestBase
{
    [Fact]
    public async Task Register_UsernameTooShort_Returns422()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "ab",
            Email = "valid@smakosz.test",
            Password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Register_UsernameInvalidChars_Returns422()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "jan kowalski!",
            Email = "valid2@smakosz.test",
            Password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Register_EmptyPassword_Returns422()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "validuser",
            Email = "valid3@smakosz.test",
            Password = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
