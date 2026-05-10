using Smakosz.Client.Ops.Pages.Admin;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages.Admin;

public class ModerationLogsTests : BunitTestBase
{
    public ModerationLogsTests()
    {
        SetAuthenticatedUser("admin", "Admin");
    }

    private static PagedResult<AdminModerationLogDto> Page(params AdminModerationLogDto[] logs) => new()
    {
        Data = logs.ToList(),
        Pagination = new PaginationInfo { Page = 1, TotalPages = 1, TotalCount = logs.Length, PageSize = 50 }
    };

    [Fact]
    public void TextModerationLog_RendersPreviewButtonAndOpensTextModal()
    {
        var admin = Services.GetRequiredService<IAdminService>();
        admin.GetModerationLogsAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(Page(new AdminModerationLogDto
            {
                LogId = 1,
                EntityType = "Review",
                EntityId = 42,
                Actor = "Ai",
                Verdict = "Rejected",
                ContentFullText = "Beznadziejne danie, nie polecam nikomu.",
                CreatedAt = DateTime.UtcNow
            }));

        var cut = RenderComponent<ModerationLogs>();
        cut.WaitForState(() => cut.Markup.Contains("Beznadziejne"));

        var previewButton = cut.FindAll("button.btn-link-sm").First();
        previewButton.TextContent.Should().Contain("Beznadziejne danie");

        cut.Markup.Should().NotContain("Treść recenzji #42");

        previewButton.Click();

        cut.Markup.Should().Contain("Treść recenzji #42");
        cut.Markup.Should().Contain("nie polecam nikomu");
    }

    [Fact]
    public void PhotoModerationLog_RendersImagePreviewButtonAndOpensImageModal()
    {
        var admin = Services.GetRequiredService<IAdminService>();
        var photoUrl = "https://assets.smakosz.xyz/photos/abc123.jpg";
        admin.GetModerationLogsAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(Page(new AdminModerationLogDto
            {
                LogId = 2,
                EntityType = "Photo",
                EntityId = 99,
                Actor = "Ai",
                Verdict = "Approved",
                ContentFullText = photoUrl,
                CreatedAt = DateTime.UtcNow
            }));

        var cut = RenderComponent<ModerationLogs>();
        cut.WaitForState(() => cut.Markup.Contains("Photo"));

        var previewButton = cut.FindAll("button.btn-link-sm").First();
        previewButton.TextContent.Should().Contain("Podgląd");

        cut.Markup.Should().NotContain("Zdjęcie #99");

        previewButton.Click();

        cut.Markup.Should().Contain("Zdjęcie #99");
        cut.Find("img[alt='Moderowane zdjęcie']").GetAttribute("src").Should().Be(photoUrl);
    }
}
