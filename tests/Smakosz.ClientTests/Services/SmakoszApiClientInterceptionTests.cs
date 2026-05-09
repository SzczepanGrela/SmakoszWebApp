using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Smakosz.Client.Models;
using Smakosz.Client.Services;

namespace Smakosz.ClientTests.Services;

public class SmakoszApiClientInterceptionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static HttpMessageHandler StubHandler(HttpStatusCode status, object? body)
    {
        var content = body != null
            ? new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
            : new StringContent("{}", Encoding.UTF8, "application/json");
        return new SingleResponseHandler(new HttpResponseMessage(status) { Content = content });
    }

    [Fact]
    public async Task ParseApiResponse_OnConcurrencyConflict_TriggersService()
    {
        var concurrency = Substitute.For<IConcurrencyConflictService>();
        var body = new ApiResponse<object>
        {
            Success = false,
            Error = new ApiError { Code = "CONCURRENCY_CONFLICT", Message = "Konflikt" }
        };
        var http = new HttpClient(StubHandler(HttpStatusCode.Conflict, body)) { BaseAddress = new Uri("https://api.test") };
        var client = new SmakoszApiClient(http, concurrency);

        var result = await client.PutApiResponseAsync<object>("/test", new { });

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("CONCURRENCY_CONFLICT");
        concurrency.Received(1).Show();
    }

    [Fact]
    public async Task ParseApiResponse_OnOtherError_DoesNotTriggerService()
    {
        var concurrency = Substitute.For<IConcurrencyConflictService>();
        var body = new ApiResponse<object>
        {
            Success = false,
            Error = new ApiError { Code = "VALIDATION_ERROR", Message = "Walidacja" }
        };
        var http = new HttpClient(StubHandler(HttpStatusCode.UnprocessableEntity, body)) { BaseAddress = new Uri("https://api.test") };
        var client = new SmakoszApiClient(http, concurrency);

        var result = await client.PutApiResponseAsync<object>("/test", new { });

        result.Error!.Code.Should().Be("VALIDATION_ERROR");
        concurrency.DidNotReceive().Show();
    }

    [Fact]
    public async Task ParseApiResponse_OnSuccess_DoesNotTriggerService()
    {
        var concurrency = Substitute.For<IConcurrencyConflictService>();
        var body = new ApiResponse<object> { Success = true };
        var http = new HttpClient(StubHandler(HttpStatusCode.OK, body)) { BaseAddress = new Uri("https://api.test") };
        var client = new SmakoszApiClient(http, concurrency);

        var result = await client.PutApiResponseAsync<object>("/test", new { });

        result.Success.Should().BeTrue();
        concurrency.DidNotReceive().Show();
    }

    private sealed class SingleResponseHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public SingleResponseHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }
}
