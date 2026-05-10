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
    private readonly IDateTimeProvider _clock;
    private readonly ScheduleNcfTrainingHandler _handler;

    public ScheduleNcfTrainingHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _ncfService = Substitute.For<INcfTrainingService>();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc));
        _ncfService.ScheduleAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _handler = new ScheduleNcfTrainingHandler(_db, _currentUser, _ncfService, _clock);
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
            .Returns(Error.Validation("NCF_INSUFFICIENT_REVIEWS", "Za malo recenzji"));

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NCF_INSUFFICIENT_REVIEWS");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ScheduleNcfTrainingHandler(_db, nonAdmin, _ncfService, _clock);

        var result = await handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NoBlockingJob_InsertsPendingRowBeforeServiceCall()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemJobs.Should().HaveCount(1);
        _sets.SystemJobs[0].Type.Should().Be("ncf_training");
        _sets.SystemJobs[0].Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task Handle_PendingJobExists_ReturnsConflict_DoesNotInsertExtra()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 1, Type = "ncf_training", Status = JobStatus.Pending, CreatedAt = new DateTime(2026, 5, 1, 10, 0, 0) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NCF_ALREADY_SCHEDULED");
        _sets.SystemJobs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_AfterInsert_CallsService()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new ScheduleNcfTrainingCommand(), CancellationToken.None);

        await _ncfService.Received(1).ScheduleAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCustomPriority_AppliesIt()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(3), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemJobs.Should().HaveCount(1);
        _sets.SystemJobs[0].Priority.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Handle_InvalidPriority_ReturnsValidationError(int invalidPriority)
    {
        var result = await _handler.Handle(new ScheduleNcfTrainingCommand(invalidPriority), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NCF_INVALID_PRIORITY");
        await _ncfService.DidNotReceive().ScheduleAsync(Arg.Any<CancellationToken>());
    }
}
