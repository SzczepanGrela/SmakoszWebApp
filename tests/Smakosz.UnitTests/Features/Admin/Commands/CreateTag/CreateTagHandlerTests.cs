using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreateTag;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreateTag;

[Trait("Category", "Handlers")]
public class CreateTagHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly CreateTagHandler _handler;

    public CreateTagHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _handler = new CreateTagHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesTagAndReturnsId()
    {
        var result = await _handler.Handle(
            new CreateTagCommand("Na wynos", "Typ", "Both", "#ff0000"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Tags.Should().HaveCount(1);
        _sets.Tags[0].TagName.Should().Be("Na wynos");
        _sets.Tags[0].Category.Should().Be("Typ");
        _sets.Tags[0].TargetEntity.Should().Be(TagTargetEntity.Both);
        _sets.Tags[0].DisplayColor.Should().Be("#ff0000");
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsAlreadyExists()
    {
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Na wynos", Category = "Typ" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateTagCommand("na wynos", "Typ", "Both", null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TAG_ALREADY_EXISTS");
    }
}
