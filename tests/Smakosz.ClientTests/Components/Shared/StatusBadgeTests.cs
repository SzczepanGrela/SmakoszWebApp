using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components.Shared;

public class StatusBadgeTests : BunitTestBase
{
    [Theory]
    [InlineData("Active", "bg-success")]
    [InlineData("Approved", "bg-success")]
    [InlineData("Completed", "bg-success")]
    [InlineData("Resolved", "bg-success")]
    [InlineData("Pending", "bg-warning")]
    [InlineData("Open", "bg-warning")]
    [InlineData("Scheduled", "bg-warning")]
    [InlineData("Rejected", "bg-danger")]
    [InlineData("Banned", "bg-danger")]
    [InlineData("Error", "bg-danger")]
    [InlineData("Training", "bg-info")]
    [InlineData("Idle", "bg-info")]
    [InlineData("Unknown", "bg-secondary")]
    public void Status_MapsToCorrectBadgeColor(string status, string expectedClass)
    {
        var cut = RenderComponent<StatusBadge>(p => p.Add(c => c.Status, status));

        var badge = cut.Find(".badge");
        badge.TextContent.Should().Be(status);
        badge.ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void CustomColor_OverridesDefault()
    {
        var cut = RenderComponent<StatusBadge>(p => p
            .Add(c => c.Status, "Active")
            .Add(c => c.Color, "bg-primary"));

        cut.Find(".badge").ClassList.Should().Contain("bg-primary");
    }
}
