using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreateJob;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreateJob;

[Trait("Category", "Handlers")]
public class CreateJobHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly CreateJobHandler _handler;

    public CreateJobHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        _handler = new CreateJobHandler(_db, _currentUser, _clock);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesJob()
    {
        var command = new CreateJobCommand("text_moderation", 5, """{"review_id":1}""", "1", "review");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemJobs.Should().HaveCount(1);
        var job = _sets.SystemJobs[0];
        job.Type.Should().Be("text_moderation");
        job.Status.Should().Be(JobStatus.Pending);
        job.Priority.Should().Be(5);
        job.Payload.Should().Be("""{"review_id":1}""");
        job.EntityId.Should().Be("1");
        job.EntityType.Should().Be("review");
        job.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new CreateJobHandler(_db, nonAdmin, _clock);

        var result = await handler.Handle(new CreateJobCommand("text_moderation", 0, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NullOptionalFields_CreatesJobWithDefaults()
    {
        var command = new CreateJobCommand("image_moderation", 0, null, null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var job = _sets.SystemJobs[0];
        job.Payload.Should().BeNull();
        job.EntityId.Should().BeNull();
        job.EntityType.Should().BeNull();
    }
}
