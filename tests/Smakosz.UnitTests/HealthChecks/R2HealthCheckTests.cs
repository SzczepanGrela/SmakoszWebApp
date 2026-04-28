using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.HealthChecks;

namespace Smakosz.UnitTests.HealthChecks;

[Trait("Category", "HealthChecks")]
public class R2HealthCheckTests
{
    private readonly IFileStorageService _storage;
    private readonly R2HealthCheck _check;
    private static readonly HealthCheckContext Context = new();

    public R2HealthCheckTests()
    {
        _storage = Substitute.For<IFileStorageService>();
        _check = new R2HealthCheck(_storage);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenConnectivityCheckSucceeds()
    {
        _storage.CheckConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _check.CheckHealthAsync(Context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenOperationCanceled()
    {
        _storage.CheckConnectivityAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());

        var result = await _check.CheckHealthAsync(Context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("timeout");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenExceptionThrown()
    {
        _storage.CheckConnectivityAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _check.CheckHealthAsync(Context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("error");
    }
}
