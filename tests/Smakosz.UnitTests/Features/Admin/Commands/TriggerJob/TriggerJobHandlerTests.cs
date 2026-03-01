using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.TriggerJob;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.TriggerJob;

[Trait("Category", "Handlers")]
public class TriggerJobHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly TriggerJobHandler _handler;

    public TriggerJobHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new TriggerJobHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ResetsJobState()
    {
        _sets.SystemJobs.Add(new SystemJob
        {
            JobId = 1, Type = "text_moderation", Status = JobStatus.Failed,
            Attempts = 3, Progress = 50, ErrorMessage = "timeout", ErrorLog = "stack trace"
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new TriggerJobCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var job = _sets.SystemJobs[0];
        job.Status.Should().Be(JobStatus.Pending);
        job.Attempts.Should().Be(0);
        job.Progress.Should().Be(0);
        job.ErrorMessage.Should().BeNull();
        job.ErrorLog.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new TriggerJobHandler(_db, nonAdmin);

        var result = await handler.Handle(new TriggerJobCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new TriggerJobCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("JOB_NOT_FOUND");
    }
}
