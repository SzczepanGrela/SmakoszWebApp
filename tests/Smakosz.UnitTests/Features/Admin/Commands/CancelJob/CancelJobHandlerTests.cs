using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CancelJob;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CancelJob;

[Trait("Category", "Handlers")]
public class CancelJobHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly CancelJobHandler _handler;

    public CancelJobHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        _handler = new CancelJobHandler(_db, _currentUser, _clock);
    }

    [Fact]
    public async Task Handle_PendingJob_CancelsSuccessfully()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 1, Type = "text_moderation", Status = JobStatus.Pending });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new CancelJobCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemJobs[0].Status.Should().Be(JobStatus.Cancelled);
        _sets.SystemJobs[0].FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ProcessingJobWithWorker_ClearsWorkerAndCancels()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 1, Type = "ncf_training", Status = JobStatus.Processing, WorkerNode = "gpu-1" });
        _sets.SystemNodes.Add(new SystemNode { NodeId = "gpu-1", CurrentJobId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new CancelJobCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemJobs[0].Status.Should().Be(JobStatus.Cancelled);
        _sets.SystemJobs[0].WorkerNode.Should().BeNull();
        _sets.SystemNodes[0].CurrentJobId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CompletedJob_ReturnsConflict()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 1, Type = "text_moderation", Status = JobStatus.Completed });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new CancelJobCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("JOB_CANNOT_CANCEL");
    }

    [Fact]
    public async Task Handle_AlreadyCancelledJob_ReturnsConflict()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 1, Type = "text_moderation", Status = JobStatus.Cancelled });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new CancelJobCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("JOB_CANNOT_CANCEL");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new CancelJobHandler(_db, nonAdmin, _clock);

        var result = await handler.Handle(new CancelJobCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new CancelJobCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("JOB_NOT_FOUND");
    }
}
