using FluentAssertions;
using Smakosz.Orchestrator.Authorization;

namespace Smakosz.UnitTests.Orchestrator.Authorization;

public sealed class TailnetDashboardAuthorizationFilterTests
{
    [Fact]
    public void Authorize_AlwaysReturnsTrue()
    {
        var filter = new TailnetDashboardAuthorizationFilter();

        filter.Authorize(null!).Should().BeTrue();
    }
}
