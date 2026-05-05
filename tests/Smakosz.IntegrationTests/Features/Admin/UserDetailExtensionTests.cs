using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Admin;

public class UserDetailExtensionTests : IntegrationTestBase
{
    private Guid _userPublicId;
    private Guid _otherUserPublicId;

    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        var user = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);
        var otherUser = SeedHelpers.CreateUser(2, "anna-nowak", "anna@smakosz.test", hash);
        _userPublicId = user.PublicId;
        _otherUserPublicId = otherUser.PublicId;

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateAdminUser(99, hash));
            db.Users.Add(user);
            db.Users.Add(otherUser);
            db.SiteStats.Add(SeedHelpers.CreateSiteStats());
            await db.SaveChangesAsync();
        });
    }

    private async Task<T?> ReadAsync<T>(Func<ISmakoszDbContext, Task<T>> read)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        return await read(db);
    }

    [Fact]
    public async Task GetUserReviews_ReturnsOk()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.Restaurants.Add(SeedHelpers.CreateRestaurant(1, "Pizzeria Roma"));
            for (var i = 1; i <= 12; i++)
            {
                db.Dishes.Add(SeedHelpers.CreateDish(i, $"Dish {i}", 1, 20m));
            }
            await db.SaveChangesAsync();

            for (var i = 1; i <= 12; i++)
            {
                db.Reviews.Add(SeedHelpers.CreateReview(i, 1, i, 1));
            }
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync($"/api/admin/users/{_userPublicId}/reviews?page=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<AdminUserReviewDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Count.Should().Be(10);
        result.Pagination.TotalCount.Should().Be(12);
        result.Pagination.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetUserReviews_UnknownUser_Returns404()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync($"/api/admin/users/{Guid.NewGuid()}/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserSecurityLogs_ReturnsOk()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.SecurityLogs.AddRange(
                new SecurityLog { EventType = SecurityEventType.FailedLogin, UserId = 1, Email = "jan@smakosz.test", CreatedAt = DateTime.UtcNow },
                new SecurityLog { EventType = SecurityEventType.FailedLogin, UserId = 1, Email = "jan@smakosz.test", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
                new SecurityLog { EventType = SecurityEventType.FailedLogin, UserId = 1, Email = "jan@smakosz.test", CreatedAt = DateTime.UtcNow.AddMinutes(-2) }
            );
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync($"/api/admin/users/{_userPublicId}/security-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<SecurityLogDto>>(response);
        result.Should().NotBeNull();
        result!.Pagination.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetUserPhotos_ReturnsOk()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.Restaurants.Add(SeedHelpers.CreateRestaurant(1, "Pizzeria Roma"));
            await db.SaveChangesAsync();

            db.MediaAssets.Add(new MediaAsset
            {
                PublicId = Guid.NewGuid(),
                EntityType = MediaEntityType.Restaurant,
                EntityId = 1,
                Url = "https://cdn.example.com/photo.jpg",
                UploadedBy = 1,
                ModerationStatus = ContentModerationStatus.Approved,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync($"/api/admin/users/{_userPublicId}/photos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<PhotoModerationDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetUserTickets_ReturnsOk()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.SystemTickets.Add(new SystemTicket
            {
                TicketType = TicketType.Contact,
                ReferenceId = 1,
                RequesterId = 1,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync($"/api/admin/users/{_userPublicId}/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<AdminTicketDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetUserActionLogs_AfterBan_ContainsBanEntry()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var banResponse = await client.PostAsync($"/api/admin/users/{_otherUserPublicId}/ban", null);
        banResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetAsync($"/api/admin/users/{_otherUserPublicId}/action-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<AdminUserActionLogDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Should().Contain(log => log.ActionType == "ban");
    }

    [Fact]
    public async Task GetUserFollowers_ReturnsOk()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.UserFollows.Add(new UserFollow
            {
                FollowerId = 2,
                FollowedId = 1,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync($"/api/admin/users/{_userPublicId}/followers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<AdminUserFollowerDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Count.Should().Be(1);
        result.Data[0].Username.Should().Be("anna-nowak");
    }

    [Fact]
    public async Task GetUserRestaurantClaims_ReturnsOk()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.Restaurants.Add(SeedHelpers.CreateRestaurant(1, "Pizzeria Roma"));
            await db.SaveChangesAsync();

            db.SystemTickets.Add(new SystemTicket
            {
                TicketType = TicketType.RestaurantClaim,
                ReferenceId = 1,
                RequesterId = 1,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync($"/api/admin/users/{_userPublicId}/restaurant-claims");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<AdminUserRestaurantClaimDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Count.Should().Be(1);
        result.Data[0].RestaurantName.Should().Be("Pizzeria Roma");
    }

    [Fact]
    public async Task ChangeEmail_Success_FlipsEmailVerified_AndWritesActionLog()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{_userPublicId}/email",
            new { Email = "newemail@smakosz.test" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var user = await ReadAsync(db =>
            db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == _userPublicId));

        user.Should().NotBeNull();
        user!.Email.Should().Be("newemail@smakosz.test");
        user.EmailVerified.Should().BeFalse();

        var log = await ReadAsync(db =>
            db.UserActionLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.UserId == 1 && l.ActionType == "email_change"));

        log.Should().NotBeNull();
    }

    [Fact]
    public async Task ChangeEmail_Conflict_Returns409()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{_userPublicId}/email",
            new { Email = "anna@smakosz.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangeUsername_Conflict_Returns409()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{_userPublicId}/username",
            new { Username = "anna-nowak" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse_AndWritesActionLog()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.PostAsync($"/api/admin/users/{_otherUserPublicId}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var user = await ReadAsync(db =>
            db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == _otherUserPublicId));

        user.Should().NotBeNull();
        user!.IsActive.Should().BeFalse();

        var log = await ReadAsync(db =>
            db.UserActionLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.UserId == 2 && l.ActionType == "deactivate"));

        log.Should().NotBeNull();
    }

    [Fact]
    public async Task Activate_SetsIsActiveTrue_AndWritesActionLog()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        await client.PostAsync($"/api/admin/users/{_otherUserPublicId}/deactivate", null);

        var response = await client.PostAsync($"/api/admin/users/{_otherUserPublicId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var user = await ReadAsync(db =>
            db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == _otherUserPublicId));

        user.Should().NotBeNull();
        user!.IsActive.Should().BeTrue();

        var log = await ReadAsync(db =>
            db.UserActionLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.UserId == 2 && l.ActionType == "activate"));

        log.Should().NotBeNull();
    }
}
