using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Smakosz.Client.Models;
using Smakosz.Client.Services;

namespace Smakosz.ClientTests;

public class SmakoszApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static SmakoszApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.com") };
        return new SmakoszApiClient(httpClient);
    }

    private static FakeHttpHandler CreateHandler(HttpStatusCode status, object? body = null)
    {
        var content = body != null
            ? new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json")
            : new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        return new FakeHttpHandler(new HttpResponseMessage(status) { Content = content });
    }

    [Fact]
    public async Task GetAsync_WithSuccessResponse_ReturnsData()
    {
        var apiResponse = new ApiResponse<string> { Success = true, Data = "hello" };
        var handler = CreateHandler(HttpStatusCode.OK, apiResponse);
        var client = CreateClient(handler);

        var result = await client.GetAsync<string>("/test");

        result.Should().Be("hello");
    }

    [Fact]
    public async Task GetAsync_WithFailedResponse_ReturnsDefault()
    {
        var handler = CreateHandler(HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        var result = await client.GetAsync<string>("/test");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithUnsuccessfulApiResponse_ReturnsDefault()
    {
        var apiResponse = new ApiResponse<string> { Success = false, Data = null };
        var handler = CreateHandler(HttpStatusCode.OK, apiResponse);
        var client = CreateClient(handler);

        var result = await client.GetAsync<string>("/test");

        result.Should().BeNull();
    }

    [Fact]
    public async Task PostAsync_WithSuccessResponse_ReturnsData()
    {
        var apiResponse = new ApiResponse<int> { Success = true, Data = 42 };
        var handler = CreateHandler(HttpStatusCode.OK, apiResponse);
        var client = CreateClient(handler);

        var result = await client.PostAsync<int>("/test", new { value = 1 });

        result.Should().Be(42);
    }

    [Fact]
    public async Task DeleteAsync_WithSuccess_ReturnsTrue()
    {
        var handler = CreateHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var result = await client.DeleteAsync("/test/1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WithFailure_ReturnsFalse()
    {
        var handler = CreateHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var result = await client.DeleteAsync("/test/1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetApiResponseAsync_WithSuccess_ReturnsFullResponse()
    {
        var apiResponse = new ApiResponse<string> { Success = true, Data = "data" };
        var handler = CreateHandler(HttpStatusCode.OK, apiResponse);
        var client = CreateClient(handler);

        var result = await client.GetApiResponseAsync<string>("/test");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("data");
    }

    [Fact]
    public async Task GetApiResponseAsync_WithServerError_ReturnsErrorResponse()
    {
        var handler = CreateHandler(HttpStatusCode.InternalServerError, "not json{{{");
        var client = CreateClient(handler);

        var result = await client.GetApiResponseAsync<string>("/test");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task PostApiResponseAsync_SendsRequestAndParsesResponse()
    {
        var apiResponse = new ApiResponse<string> { Success = true, Data = "created" };
        var handler = CreateHandler(HttpStatusCode.Created, apiResponse);
        var client = CreateClient(handler);

        var result = await client.PostApiResponseAsync<string>("/test", new { name = "test" });

        result.Success.Should().BeTrue();
        result.Data.Should().Be("created");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task PutAsync_WithSuccess_ReturnsData()
    {
        var apiResponse = new ApiResponse<string> { Success = true, Data = "updated" };
        var handler = CreateHandler(HttpStatusCode.OK, apiResponse);
        var client = CreateClient(handler);

        var result = await client.PutAsync<string>("/test/1", new { name = "new" });

        result.Should().Be("updated");
    }

    [Fact]
    public async Task DeleteApiResponseAsync_ReturnsApiResponse()
    {
        var apiResponse = new ApiResponse<object> { Success = true };
        var handler = CreateHandler(HttpStatusCode.OK, apiResponse);
        var client = CreateClient(handler);

        var result = await client.DeleteApiResponseAsync("/test/1");

        result.Success.Should().BeTrue();
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }
}
