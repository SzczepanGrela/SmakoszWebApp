using System.Net;

namespace Smakosz.UnitTests.Common.TestInfrastructure;

public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHttpMessageHandler(HttpStatusCode status)
        : this((_, _) => Task.FromResult(new HttpResponseMessage(status)))
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public static StubHttpMessageHandler Throws(Exception ex) =>
        new((_, _) => throw ex);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return _handler(request, cancellationToken);
    }
}
