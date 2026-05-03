using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.WakeGpu;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.WakeGpu;

[Trait("Category", "Handlers")]
public class WakeGpuHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IGpuWakeService _gpuWake;

    public WakeGpuHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _gpuWake = Substitute.For<IGpuWakeService>();
    }

    private WakeGpuHandler CreateHandler(ICurrentUserService user) => new(_gpuWake, user, _db);

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden_NoCallToService()
    {
        var user = MockExtensions.CreateAuthenticatedUser(role: "User");
        var handler = CreateHandler(user);

        var result = await handler.Handle(new WakeGpuCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
        await _gpuWake.DidNotReceive().WakeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StatusSent_WritesAuditLog_AndReturnsResult()
    {
        var admin = MockExtensions.CreateAdminUser(userId: 99);
        _gpuWake.WakeAsync(Arg.Any<CancellationToken>())
            .Returns(new GpuWakeResult(GpuWakeStatus.Sent));
        var handler = CreateHandler(admin);

        var result = await handler.Handle(new WakeGpuCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(GpuWakeStatus.Sent);
        _sets.AuditLogs.Should().ContainSingle(a =>
            a.TableName == "system_nodes"
            && a.ChangedBy == "99"
            && a.NewValues!.Contains("manual_admin_panel"));
        await _db.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StatusAlreadyOnline_DoesNotWriteAuditLog()
    {
        var admin = MockExtensions.CreateAdminUser();
        _gpuWake.WakeAsync(Arg.Any<CancellationToken>())
            .Returns(new GpuWakeResult(GpuWakeStatus.AlreadyOnline));
        var handler = CreateHandler(admin);

        var result = await handler.Handle(new WakeGpuCommand(), CancellationToken.None);

        result.Value.Status.Should().Be(GpuWakeStatus.AlreadyOnline);
        _sets.AuditLogs.Should().BeEmpty();
        await _db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StatusGatewayFailed_DoesNotWriteAuditLog_PassesMessageThrough()
    {
        var admin = MockExtensions.CreateAdminUser();
        _gpuWake.WakeAsync(Arg.Any<CancellationToken>())
            .Returns(new GpuWakeResult(GpuWakeStatus.GatewayFailed, "boom"));
        var handler = CreateHandler(admin);

        var result = await handler.Handle(new WakeGpuCommand(), CancellationToken.None);

        result.Value.Status.Should().Be(GpuWakeStatus.GatewayFailed);
        result.Value.Message.Should().Be("boom");
        _sets.AuditLogs.Should().BeEmpty();
    }
}
