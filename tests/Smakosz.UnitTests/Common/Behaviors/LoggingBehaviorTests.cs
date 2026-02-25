using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Smakosz.Application.Common.Behaviors;

namespace Smakosz.UnitTests.Common.Behaviors;

public record LoggingTestRequest(string Name) : IRequest<string>;

[Trait("Category", "Behaviors")]
public class LoggingBehaviorTests
{
    private readonly ILogger<LoggingBehavior<LoggingTestRequest, string>> _logger;
    private readonly LoggingBehavior<LoggingTestRequest, string> _behavior;

    public LoggingBehaviorTests()
    {
        _logger = Substitute.For<ILogger<LoggingBehavior<LoggingTestRequest, string>>>();
        _behavior = new LoggingBehavior<LoggingTestRequest, string>(_logger);
    }

    [Fact]
    public async Task Handle_LogsRequestName()
    {
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns("result");

        var result = await _behavior.Handle(new LoggingTestRequest("test"), next, CancellationToken.None);

        result.Should().Be("result");
        _logger.ReceivedWithAnyArgs(1).Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_FastRequest_NoWarning()
    {
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns("result");

        await _behavior.Handle(new LoggingTestRequest("test"), next, CancellationToken.None);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_SlowRequest_LogsWarning()
    {
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(async _ =>
        {
            await Task.Delay(600);
            return "result";
        });

        await _behavior.Handle(new LoggingTestRequest("test"), next, CancellationToken.None);

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
