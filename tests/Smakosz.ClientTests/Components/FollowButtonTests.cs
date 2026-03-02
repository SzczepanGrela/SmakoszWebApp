using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class FollowButtonTests : BunitTestBase
{
    [Fact]
    public void NotFollowing_ShowsObserwuj()
    {
        var cut = RenderComponent<FollowButton>(p => p.Add(c => c.IsFollowing, false));

        cut.Find("button").TextContent.Should().Contain("Obserwuj");
        cut.Find("button").ClassList.Should().Contain("btn-primary");
        cut.Find("i").ClassList.Should().Contain("fa-user-plus");
    }

    [Fact]
    public void Following_ShowsObserwujesz()
    {
        var cut = RenderComponent<FollowButton>(p => p.Add(c => c.IsFollowing, true));

        cut.Find("button").TextContent.Should().Contain("Obserwujesz");
        cut.Find("button").ClassList.Should().Contain("btn-outline-secondary");
        cut.Find("i").ClassList.Should().Contain("fa-user-check");
    }

    [Fact]
    public void Click_InvokesOnToggle()
    {
        var toggled = false;
        var cut = RenderComponent<FollowButton>(p => p
            .Add(c => c.IsFollowing, false)
            .Add(c => c.OnToggle, () => toggled = true));

        cut.Find("button").Click();
        toggled.Should().BeTrue();
    }
}
