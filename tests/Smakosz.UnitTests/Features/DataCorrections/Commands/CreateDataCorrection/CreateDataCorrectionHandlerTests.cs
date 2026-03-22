using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.DataCorrections.Commands.CreateDataCorrection;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.DataCorrections.Commands.CreateDataCorrection;

[Trait("Category", "Handlers")]
public class CreateDataCorrectionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublicConfigProvider _configProvider;
    private readonly CreateDataCorrectionHandler _handler;

    public CreateDataCorrectionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _configProvider = Substitute.For<IPublicConfigProvider>();
        _configProvider.GetIntAsync("datacorrection.response_deadline_days", 7, Arg.Any<CancellationToken>()).Returns(7);
        _handler = new CreateDataCorrectionHandler(_db, _currentUser, _configProvider);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesCorrectionAndTicket()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("bella-italia").Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateDataCorrectionCommand("bella-italia", "Address", "Wrong address", "Correct address"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.DataCorrectionRequests.Should().HaveCount(1);
        _sets.SystemTickets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new CreateDataCorrectionCommand("nonexistent", "Address", null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new CreateDataCorrectionHandler(_db, anonymous, _configProvider);

        var result = await handler.Handle(
            new CreateDataCorrectionCommand("slug", "WrongAddress", null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_InvalidIssueType_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("bella-italia").Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateDataCorrectionCommand("bella-italia", "INVALID_TYPE_XYZ", null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("INVALID_ISSUE_TYPE");
    }
}
