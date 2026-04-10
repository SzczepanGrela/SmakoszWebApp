using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetAdminRestaurantDetail;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAdminRestaurantDetail;

[Trait("Category", "Handlers")]
public class GetAdminRestaurantDetailHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAdminRestaurantDetailHandler _handler;

    public GetAdminRestaurantDetailHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetAdminRestaurantDetailHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ExistingRestaurant_ReturnsDetailDto()
    {
        _sets.Restaurants.Add(new RestaurantBuilder()
            .WithId(42)
            .WithName("Bella Italia")
            .WithSlug("bella-italia")
            .WithCuisineType("Italian")
            .Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminRestaurantDetailQuery(42), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.RestaurantId.Should().Be(42);
        result.Value.Name.Should().Be("Bella Italia");
        result.Value.Slug.Should().Be("bella-italia");
        result.Value.CuisineType.Should().Be("Italian");
        result.Value.Status.Should().Be(RestaurantStatus.Active.ToString());
    }

    [Fact]
    public async Task Handle_NonExistingRestaurant_ReturnsNotFound()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminRestaurantDetailQuery(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAdminRestaurantDetailHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetAdminRestaurantDetailQuery(42), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_CountsOnlyNonDeletedReviews()
    {
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(1).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(1).WithRestaurantId(1).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(2).WithRestaurantId(1).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(3).WithRestaurantId(1).AsDeleted().Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(4).WithRestaurantId(2).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminRestaurantDetailQuery(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ReviewCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_LimitsRecentReviewsToFive()
    {
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(1).Build());
        for (var i = 1; i <= 8; i++)
        {
            _sets.Reviews.Add(new ReviewBuilder()
                .WithId(i)
                .WithRestaurantId(1)
                .WithCreatedAt(DateTime.UtcNow.AddMinutes(-i))
                .Build());
        }
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminRestaurantDetailQuery(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.RecentReviews.Should().HaveCount(5);
        result.Value.ReviewCount.Should().Be(8);
    }

    [Fact]
    public async Task Handle_AggregatesCounters()
    {
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(1).Build());
        _sets.MenuSections.Add(new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Starters" });
        _sets.MenuSections.Add(new MenuSection { SectionId = 2, RestaurantId = 1, SectionName = "Mains" });
        _sets.MenuSections.Add(new MenuSection { SectionId = 3, RestaurantId = 2, SectionName = "Other" });

        _sets.Dishes.Add(new DishBuilder().WithId(1).WithRestaurantId(1).Build());
        _sets.Dishes.Add(new DishBuilder().WithId(2).WithRestaurantId(1).Build());
        _sets.Dishes.Add(new DishBuilder().WithId(3).WithRestaurantId(2).Build());

        _sets.RestaurantEditRequests.Add(new RestaurantEditRequest
        {
            RequestId = 1,
            RestaurantId = 1,
            UserId = 1,
            ChangeType = EditRequestChangeType.InfoUpdate,
            Payload = "{}",
            Status = EditRequestStatus.Pending
        });
        _sets.RestaurantEditRequests.Add(new RestaurantEditRequest
        {
            RequestId = 2,
            RestaurantId = 1,
            UserId = 1,
            ChangeType = EditRequestChangeType.InfoUpdate,
            Payload = "{}",
            Status = EditRequestStatus.Approved
        });

        _sets.MediaAssets.Add(new MediaAsset
        {
            AssetId = 1,
            EntityType = MediaEntityType.Restaurant,
            EntityId = 1,
            Url = "a.webp",
            ModerationStatus = ContentModerationStatus.Pending
        });
        _sets.MediaAssets.Add(new MediaAsset
        {
            AssetId = 2,
            EntityType = MediaEntityType.Restaurant,
            EntityId = 1,
            Url = "b.webp",
            ModerationStatus = ContentModerationStatus.Approved
        });
        _sets.MediaAssets.Add(new MediaAsset
        {
            AssetId = 3,
            EntityType = MediaEntityType.Restaurant,
            EntityId = 2,
            Url = "c.webp",
            ModerationStatus = ContentModerationStatus.Approved
        });

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminRestaurantDetailQuery(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MenuSectionsCount.Should().Be(2);
        result.Value.MenuItemsCount.Should().Be(2);
        result.Value.PendingEditRequestsCount.Should().Be(1);
        result.Value.PendingPhotosCount.Should().Be(1);
        result.Value.ApprovedPhotosCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PopulatesOpeningHoursSortedByDay()
    {
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(1).Build());
        _sets.OpeningHours.Add(new RestaurantOpeningHours
        {
            HoursId = 1,
            RestaurantId = 1,
            DayOfWeek = 3,
            OpenTime = new TimeOnly(10, 0),
            CloseTime = new TimeOnly(22, 0),
            IsClosed = false
        });
        _sets.OpeningHours.Add(new RestaurantOpeningHours
        {
            HoursId = 2,
            RestaurantId = 1,
            DayOfWeek = 1,
            OpenTime = new TimeOnly(9, 0),
            CloseTime = new TimeOnly(21, 0),
            IsClosed = false
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminRestaurantDetailQuery(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.OpeningHours.Should().HaveCount(2);
        result.Value.OpeningHours[0].DayOfWeek.Should().Be(1);
        result.Value.OpeningHours[1].DayOfWeek.Should().Be(3);
    }
}
