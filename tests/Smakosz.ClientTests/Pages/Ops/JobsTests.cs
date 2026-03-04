using Smakosz.Client.Ops.Pages.Admin;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages.Ops;

public class JobsTests : BunitTestBase
{
    public JobsTests()
    {
        SetAuthenticatedUser("admin", "Admin");
    }

    private static PagedResult<AdminJobDto> CreateJobsPage() => new()
    {
        Data =
        [
            new AdminJobDto
            {
                JobId = 1, Type = "text_moderation", Status = "Completed",
                Priority = 1, Progress = 100,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                FinishedAt = DateTime.UtcNow.AddHours(-1)
            },
            new AdminJobDto
            {
                JobId = 2, Type = "ncf_training", Status = "Processing",
                Priority = 2, Progress = 45,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            },
            new AdminJobDto
            {
                JobId = 3, Type = "image_moderation", Status = "Pending",
                Priority = 1, Progress = 0,
                CreatedAt = DateTime.UtcNow
            }
        ],
        Pagination = new PaginationInfo { Page = 1, TotalPages = 2, TotalCount = 5, PageSize = 3 }
    };

    [Fact]
    public void LoadingState_ShowsSpinner()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(new TaskCompletionSource<PagedResult<AdminJobDto>?>().Task);

        var cut = RenderComponent<Jobs>();
        cut.Find(".spinner-border").Should().NotBeNull();
    }

    [Fact]
    public void NoJobs_ShowsEmptyState()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(new PagedResult<AdminJobDto>
        {
            Data = [],
            Pagination = new PaginationInfo()
        });

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("Brak zadan"));
    }

    [Fact]
    public void JobsLoaded_ShowsTable()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.Markup.Should().Contain("text_moderation");
        cut.Markup.Should().Contain("ncf_training");
        cut.Markup.Should().Contain("image_moderation");
        cut.FindAll("tbody tr").Should().HaveCount(3);
    }

    [Fact]
    public void PendingJob_ShowsCancelButton()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.FindAll("button[title='Anuluj']").Should().HaveCount(2);
    }

    [Fact]
    public async Task TriggerJob_CallsService()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());
        adminService.TriggerJobAsync(1).Returns(true);

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.FindAll("button[title='Uruchom ponownie']")[0].Click();

        await adminService.Received(1).TriggerJobAsync(1);
    }

    [Fact]
    public async Task CancelJob_CallsService()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());
        adminService.CancelJobAsync(Arg.Any<int>()).Returns(true);

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.FindAll("button[title='Anuluj']")[0].Click();

        await adminService.Received(1).CancelJobAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task ScheduleNcfTraining_CallsService()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());
        adminService.ScheduleNcfTrainingAsync().Returns(true);

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.FindAll("button").First(b => b.TextContent.Contains("NCF Training")).Click();

        await adminService.Received(1).ScheduleNcfTrainingAsync();
    }

    [Fact]
    public void ShowCreateModal_OpensModal()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Nowe zadanie")).Click();

        cut.Markup.Should().Contain("Nowe zadanie");
        cut.Find("select.form-select").Should().NotBeNull();
    }

    [Fact]
    public async Task CreateJob_CallsService()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());
        adminService.CreateJobAsync(Arg.Any<CreateJobRequest>()).Returns(true);

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Nowe zadanie")).Click();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Utworz").Click();

        await adminService.Received(1).CreateJobAsync(Arg.Any<CreateJobRequest>());
    }

    [Fact]
    public void Pagination_ShowsWhenMultiplePages()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetJobsAsync(Arg.Any<int>()).Returns(CreateJobsPage());

        var cut = RenderComponent<Jobs>();
        cut.WaitForState(() => cut.Markup.Contains("text_moderation"));

        cut.Find("nav").Should().NotBeNull();
    }
}
