using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class UserCardTests : BunitTestBase
{
    private static UserSummaryDto CreateUser() => new()
    {
        PublicId = Guid.NewGuid(),
        Slug = "jan-kowalski",
        Username = "JanKowalski",
        ReviewCount = 15
    };

    [Fact]
    public void RendersUsernameAndReviewCount()
    {
        var user = CreateUser();
        var cut = RenderComponent<UserCard>(p => p.Add(c => c.User, user));

        cut.Markup.Should().Contain("JanKowalski");
        cut.Markup.Should().Contain("15 recenzji");
        cut.Find("a[href='/users/jan-kowalski']").Should().NotBeNull();
    }

    [Fact]
    public void ShowFollowButtonFalse_NoFollowButton()
    {
        var user = CreateUser();
        var cut = RenderComponent<UserCard>(p => p
            .Add(c => c.User, user)
            .Add(c => c.ShowFollowButton, false));

        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void ShowFollowButtonTrue_RendersFollowButton()
    {
        var user = CreateUser();
        var cut = RenderComponent<UserCard>(p => p
            .Add(c => c.User, user)
            .Add(c => c.ShowFollowButton, true)
            .Add(c => c.IsFollowing, false));

        cut.Find("button").TextContent.Should().Contain("Obserwuj");
    }

    [Fact]
    public async Task ClickFollow_CallsService()
    {
        var user = CreateUser();
        var userProfileService = Services.GetRequiredService<IUserProfileService>();
        userProfileService.FollowUserAsync("jan-kowalski").Returns(true);

        var cut = RenderComponent<UserCard>(p => p
            .Add(c => c.User, user)
            .Add(c => c.ShowFollowButton, true)
            .Add(c => c.IsFollowing, false));

        cut.Find("button").Click();

        await userProfileService.Received(1).FollowUserAsync("jan-kowalski");
    }

    [Fact]
    public async Task ClickUnfollow_CallsService()
    {
        var user = CreateUser();
        var userProfileService = Services.GetRequiredService<IUserProfileService>();
        userProfileService.UnfollowUserAsync("jan-kowalski").Returns(true);

        var cut = RenderComponent<UserCard>(p => p
            .Add(c => c.User, user)
            .Add(c => c.ShowFollowButton, true)
            .Add(c => c.IsFollowing, true));

        cut.Find("button").Click();

        await userProfileService.Received(1).UnfollowUserAsync("jan-kowalski");
    }
}
