using Smakosz.Client.Ops.Pages.Admin;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages.Admin;

public class NcfStatusTests : BunitTestBase
{
    public NcfStatusTests()
    {
        SetAuthenticatedUser("admin", "Admin");
    }

    [Fact]
    public void Loading_ShowsSpinner()
    {
        var admin = Services.GetRequiredService<IAdminService>();
        admin.GetNcfStatusAsync().Returns(new TaskCompletionSource<AdminNcfStatusDto?>().Task);

        var cut = RenderComponent<NcfStatus>();

        cut.Markup.Should().Contain("Ładowanie statusu NCF");
    }

    [Fact]
    public void NcfAvailable_RendersGreenCardsAndVersion()
    {
        var admin = Services.GetRequiredService<IAdminService>();
        admin.GetNcfStatusAsync().Returns(new AdminNcfStatusDto
        {
            NcfAvailable = true,
            LoadedVersion = "v20260513_004159",
            MappedUsersCount = 100,
            CachePopulatedCount = 95,
            CachePopulatedPercent = 95.0,
            LastTraining = new AdminNcfTrainingSummaryDto
            {
                JobId = 42,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                FinishedAt = DateTime.UtcNow.AddDays(-1).AddSeconds(300),
                DurationSeconds = 300,
                WorkerNode = "gpu-homelab"
            },
            RecentTrainings = []
        });

        var cut = RenderComponent<NcfStatus>();

        cut.Markup.Should().Contain("TAK");
        cut.Markup.Should().Contain("v20260513_004159");
        cut.Markup.Should().Contain("95");
        cut.Markup.Should().Contain("gpu-homelab");
        cut.Markup.Should().NotContain("is-attention");
    }

    [Fact]
    public void NcfUnavailable_ShowsFallbackReasonAndAttention()
    {
        var admin = Services.GetRequiredService<IAdminService>();
        admin.GetNcfStatusAsync().Returns(new AdminNcfStatusDto
        {
            NcfAvailable = false,
            FallbackReason = "Model NCF nie został jeszcze pobrany.",
            LoadedVersion = string.Empty,
            MappedUsersCount = 0,
            CachePopulatedCount = 0,
            CachePopulatedPercent = 0,
            RecentTrainings = []
        });

        var cut = RenderComponent<NcfStatus>();

        cut.Markup.Should().Contain("NIE");
        cut.Markup.Should().Contain("Model NCF nie został jeszcze pobrany.");
        cut.Markup.Should().Contain("is-attention");
    }

    [Fact]
    public void RecentTrainings_RendersTableRows()
    {
        var admin = Services.GetRequiredService<IAdminService>();
        admin.GetNcfStatusAsync().Returns(new AdminNcfStatusDto
        {
            NcfAvailable = true,
            LoadedVersion = "v1",
            MappedUsersCount = 10,
            CachePopulatedCount = 10,
            CachePopulatedPercent = 100,
            RecentTrainings =
            [
                new AdminNcfTrainingSummaryDto { JobId = 1, Status = "Completed", CreatedAt = DateTime.UtcNow, WorkerNode = "gpu-homelab", DurationSeconds = 100 },
                new AdminNcfTrainingSummaryDto { JobId = 2, Status = "Failed", CreatedAt = DateTime.UtcNow, WorkerNode = "gpu-homelab", ErrorMessage = "OOM" },
                new AdminNcfTrainingSummaryDto { JobId = 3, Status = "Completed", CreatedAt = DateTime.UtcNow, WorkerNode = "gpu-homelab", DurationSeconds = 250 }
            ]
        });

        var cut = RenderComponent<NcfStatus>();

        var rows = cut.FindAll("tbody tr");
        rows.Count.Should().Be(3);
        cut.Markup.Should().Contain("OOM");
    }
}
