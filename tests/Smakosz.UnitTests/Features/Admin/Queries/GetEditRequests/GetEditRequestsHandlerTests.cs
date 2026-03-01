using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetEditRequests;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetEditRequests;

[Trait("Category", "Handlers")]
public class GetEditRequestsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetEditRequestsHandler _handler;

    public GetEditRequestsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetEditRequestsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPendingEditRequests()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var user = new UserBuilder().WithId(1).Build();
        _sets.RestaurantEditRequests.Add(new RestaurantEditRequest
        {
            RequestId = 1, RestaurantId = 1, UserId = 1,
            ChangeType = EditRequestChangeType.General,
            Payload = "{}", Status = EditRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Restaurant = restaurant, User = user
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetEditRequestsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetEditRequestsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetEditRequestsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
