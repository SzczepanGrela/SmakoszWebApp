using Smakosz.Client.Ops.Pages.Admin;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages.Ops;

public class TagsTests : BunitTestBase
{
    public TagsTests()
    {
        SetAuthenticatedUser("admin", "Admin");
    }

    private static PagedResult<AdminTagDto> CreateTagsPage() => new()
    {
        Data =
        [
            new AdminTagDto
            {
                TagId = 1, TagName = "Na wynos", Category = "Typ",
                TargetEntity = "Both", DisplayColor = "#ff0000", UsageCount = 5
            },
            new AdminTagDto
            {
                TagId = 2, TagName = "Sezonowe", Category = "Typ",
                TargetEntity = "Dish", UsageCount = 2
            }
        ],
        Pagination = new PaginationInfo { Page = 1, TotalPages = 1, TotalCount = 2, PageSize = 20 }
    };

    [Fact]
    public void LoadingState_ShowsSpinner()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetTagsAsync(Arg.Any<int>(), Arg.Any<string?>()).Returns(new TaskCompletionSource<PagedResult<AdminTagDto>?>().Task);

        var cut = RenderComponent<Tags>();
        cut.Find(".spinner-border").Should().NotBeNull();
    }

    [Fact]
    public void TagsLoaded_ShowsTable()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetTagsAsync(Arg.Any<int>(), Arg.Any<string?>()).Returns(CreateTagsPage());

        var cut = RenderComponent<Tags>();
        cut.WaitForState(() => cut.Markup.Contains("Na wynos"));

        cut.Markup.Should().Contain("Na wynos");
        cut.Markup.Should().Contain("Sezonowe");
        cut.FindAll("tbody tr").Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteTag_CallsService()
    {
        var adminService = Services.GetRequiredService<IAdminService>();
        adminService.GetTagsAsync(Arg.Any<int>(), Arg.Any<string?>()).Returns(CreateTagsPage());
        adminService.DeleteTagAsync(Arg.Any<int>()).Returns(true);

        var cut = RenderComponent<Tags>();
        cut.WaitForState(() => cut.Markup.Contains("Na wynos"));

        cut.FindAll("button.btn-outline-danger")[0].Click();

        await adminService.Received(1).DeleteTagAsync(Arg.Any<int>());
    }
}
