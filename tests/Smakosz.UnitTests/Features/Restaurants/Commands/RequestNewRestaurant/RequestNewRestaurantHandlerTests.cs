using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Restaurants.Commands.RequestNewRestaurant;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Restaurants.Commands.RequestNewRestaurant;

[Trait("Category", "Handlers")]
public class RequestNewRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _user;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly RequestNewRestaurantHandler _handler;

    public RequestNewRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _user = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new RequestNewRestaurantHandler(_db, _user, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesRequestTicketWithJsonPayload()
    {
        var result = await _handler.Handle(
            new RequestNewRestaurantCommand(
                Name: "Pizzeria Bella",
                Address: "ul. Słowackiego 5, Krakow",
                Phone: "+48111222333",
                Email: "kontakt@bella.pl",
                Description: "Włoska kuchnia",
                CityId: 1,
                CuisineTypeId: 2),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets.Should().ContainSingle();
        var ticket = _sets.SystemTickets[0];
        ticket.TicketType.Should().Be(TicketType.RestaurantRequest);
        ticket.RequesterId.Should().Be(5);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.Description.Should().NotBeNull();
        ticket.Description!.Should().Contain("Pizzeria Bella");
        ticket.Description.Should().Contain("ul. Słowackiego 5");
    }

    [Fact]
    public async Task Handle_ForbiddenWordInName_ReturnsErrorAndWritesNothing()
    {
        _forbiddenWords.ContainsAsync("Sklep z chamstwem", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(
            new RequestNewRestaurantCommand(
                Name: "Sklep z chamstwem",
                Address: "ul. Testowa 1",
                Phone: null, Email: null, Description: null,
                CityId: null, CuisineTypeId: null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_CONTENT");
        _sets.SystemTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForbiddenWordInDescription_ReturnsErrorAndWritesNothing()
    {
        _forbiddenWords.ContainsAsync("Bistro", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(false);
        _forbiddenWords.ContainsAsync("opis z chamstwem", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(
            new RequestNewRestaurantCommand(
                Name: "Bistro",
                Address: "ul. Testowa 1",
                Phone: null, Email: null,
                Description: "opis z chamstwem",
                CityId: null, CuisineTypeId: null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_CONTENT");
        _sets.SystemTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PendingRequestByUser_ReturnsConflict()
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1,
            TicketType = TicketType.RestaurantRequest,
            RequesterId = 5,
            Status = TicketStatus.Open
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RequestNewRestaurantCommand(
                Name: "Bistro",
                Address: "ul. Testowa 1",
                Phone: null, Email: null, Description: null,
                CityId: null, CuisineTypeId: null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REQUEST_ALREADY_PENDING");
        _sets.SystemTickets.Should().HaveCount(1);
    }
}
