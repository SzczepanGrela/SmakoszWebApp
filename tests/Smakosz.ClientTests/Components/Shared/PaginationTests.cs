using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components.Shared;

public class PaginationTests : BunitTestBase
{
    [Fact]
    public void SinglePage_RendersNothing()
    {
        var info = new PaginationInfo { Page = 1, TotalPages = 1 };
        var cut = RenderComponent<Pagination>(p => p.Add(c => c.Info, info));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void NullInfo_RendersNothing()
    {
        var cut = RenderComponent<Pagination>();
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void MultiplePages_RendersPagination()
    {
        var info = new PaginationInfo { Page = 2, TotalPages = 5 };
        var cut = RenderComponent<Pagination>(p => p.Add(c => c.Info, info));

        cut.Find("nav").Should().NotBeNull();
        cut.FindAll(".page-item").Should().NotBeEmpty();
    }

    [Fact]
    public void ActivePage_HasActiveClass()
    {
        var info = new PaginationInfo { Page = 3, TotalPages = 5 };
        var cut = RenderComponent<Pagination>(p => p.Add(c => c.Info, info));

        var activeItem = cut.Find(".page-item.active .page-link");
        activeItem.TextContent.Trim().Should().Be("3");
    }

    [Fact]
    public void FirstPage_PrevDisabled()
    {
        var info = new PaginationInfo { Page = 1, TotalPages = 3 };
        var cut = RenderComponent<Pagination>(p => p.Add(c => c.Info, info));

        cut.FindAll(".page-item").First().ClassList.Should().Contain("disabled");
    }

    [Fact]
    public void LastPage_NextDisabled()
    {
        var info = new PaginationInfo { Page = 3, TotalPages = 3 };
        var cut = RenderComponent<Pagination>(p => p.Add(c => c.Info, info));

        cut.FindAll(".page-item").Last().ClassList.Should().Contain("disabled");
    }

    [Fact]
    public void ClickPage_InvokesOnPageChange()
    {
        int? clickedPage = null;
        var info = new PaginationInfo { Page = 1, TotalPages = 5 };
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Info, info)
            .Add(c => c.OnPageChange, (int page) => clickedPage = page));

        var pageLinks = cut.FindAll(".page-link");
        var page2 = pageLinks.First(l => l.TextContent.Trim() == "2");
        page2.Click();

        clickedPage.Should().Be(2);
    }
}
