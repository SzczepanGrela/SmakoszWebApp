using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateRestaurantAdmin;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateRestaurantAdmin;

[Trait("Category", "Handlers")]
public class UpdateRestaurantAdminHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly UpdateRestaurantAdminHandler _handler;
    private static readonly Guid TestPublicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public UpdateRestaurantAdminHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new UpdateRestaurantAdminHandler(_db, _currentUser, _forbiddenWords);
    }

    private Restaurant CreateRestaurant(int version = 1)
    {
        var r = new Restaurant
        {
            RestaurantId = 1,
            PublicId = TestPublicId,
            RestaurantName = "Old Name",
            Description = "Old desc",
            Slug = "old-name",
            Version = version,
            Status = RestaurantStatus.Active,
            CuisineTypeId = 1
        };
        _sets.Restaurants.Add(r);
        DbContextMockFactory.Refresh(_db, _sets);
        return r;
    }

    [Fact]
    public async Task Handle_UpdatesName()
    {
        var r = CreateRestaurant();

        var result = await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "New Name", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        r.RestaurantName.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_SkipsNullFields()
    {
        var r = CreateRestaurant();

        var result = await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, null, null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        r.RestaurantName.Should().Be("Old Name");
        r.Description.Should().Be("Old desc");
    }

    [Fact]
    public async Task Handle_IncrementsVersion()
    {
        var r = CreateRestaurant(version: 3);

        var result = await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "X", null, null, null, null, null, null, null, null, null, 3),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        r.Version.Should().Be(4);
    }

    [Fact]
    public async Task Handle_VersionMismatch_ReturnsConflict()
    {
        CreateRestaurant(version: 2);

        var result = await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "X", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_VERSION_MISMATCH");
    }

    [Fact]
    public async Task Handle_ForbiddenWordInName_ReturnsError()
    {
        CreateRestaurant();
        _forbiddenWords.ContainsAsync("BadWord", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "BadWord", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_CONTENT");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new UpdateRestaurantAdminHandler(_db, nonAdmin, _forbiddenWords);

        var result = await handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "X", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new UpdateRestaurantAdminCommand(Guid.NewGuid(), "X", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_CreatesAuditLogEntry()
    {
        CreateRestaurant();

        await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "New", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].TableName.Should().Be("restaurants");
        _sets.AuditLogs[0].RecordId.Should().Be(1);
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Update);
    }

    [Fact]
    public async Task Handle_WithOwner_SendsNotification()
    {
        var r = CreateRestaurant();
        r.OwnerId = 10;
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "New", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        _sets.Notifications.Should().ContainSingle();
        _sets.Notifications[0].UserId.Should().Be(10);
    }

    [Fact]
    public async Task Handle_WithoutOwner_NoNotification()
    {
        CreateRestaurant();

        await _handler.Handle(
            new UpdateRestaurantAdminCommand(TestPublicId, "New", null, null, null, null, null, null, null, null, null, 1),
            CancellationToken.None);

        _sets.Notifications.Should().BeEmpty();
    }
}
