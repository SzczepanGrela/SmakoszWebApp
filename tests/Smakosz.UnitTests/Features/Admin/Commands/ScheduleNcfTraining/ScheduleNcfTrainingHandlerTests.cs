using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ScheduleNcfTraining;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.ScheduleNcfTraining;

[Trait("Category", "Handlers")]
public class ScheduleNcfTrainingHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly INcfTrainingService _ncfService;
    private readonly ScheduleNcfTrainingHandler _handler;

    public ScheduleNcfTrainingHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _ncfService = Substitute.For<INcfTrainingService>();
        _ncfService.ScheduleAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _handler = new ScheduleNcfTrainingHandler(_db, _currentUser, _ncfService);
    }

    [Fact]
    public async Task Handle_NoPendingJobs_SchedulesTraining()
    {
        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _ncfService.Received(1).ScheduleAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingJobExists_ReturnsConflict()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 42, Type = "ncf_training", Status = JobStatus.Pending, CreatedAt = new DateTime(2026, 3, 9, 22, 0, 0) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NCF_ALREADY_SCHEDULED");
        result.FirstError.Description.Should().Contain("Job #42");
        result.FirstError.Description.Should().Contain("oczekujący");
        await _ncfService.DidNotReceive().ScheduleAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProcessingJobExists_ReturnsConflict()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 7, Type = "ncf_training", Status = JobStatus.Processing, CreatedAt = new DateTime(2026, 3, 10, 14, 30, 0) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NCF_ALREADY_SCHEDULED");
        result.FirstError.Description.Should().Contain("Job #7");
        result.FirstError.Description.Should().Contain("w trakcie");
    }

    [Fact]
    public async Task Handle_CompletedJobExists_SchedulesTraining()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 1, Type = "ncf_training", Status = JobStatus.Completed });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _ncfService.Received(1).ScheduleAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InsufficientReviews_PropagatesError()
    {
        _ncfService.ScheduleAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Validation("NCF_INSUFFICIENT_REVIEWS", "Za mało recenzji"));

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NCF_INSUFFICIENT_REVIEWS");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ScheduleNcfTrainingHandler(_db, nonAdmin, _ncfService);

        var result = await handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
