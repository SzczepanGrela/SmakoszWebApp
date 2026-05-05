using Hangfire.Dashboard;

namespace Smakosz.Orchestrator.Authorization;

public sealed class TailnetDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
