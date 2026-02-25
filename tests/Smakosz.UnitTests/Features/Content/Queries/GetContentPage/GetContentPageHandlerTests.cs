using FluentAssertions;
using Smakosz.Application.Features.Content.Queries.GetContentPage;

namespace Smakosz.UnitTests.Features.Content.Queries.GetContentPage;

[Trait("Category", "Handlers")]
public class GetContentPageHandlerTests
{
    private readonly GetContentPageHandler _handler = new();

    [Fact]
    public async Task Handle_ExistingPage_ReturnsContent()
    {
        var result = await _handler.Handle(new GetContentPageQuery("about"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Title.Should().Be("O nas");
    }

    [Theory]
    [InlineData("terms")]
    [InlineData("contact")]
    public async Task Handle_AllStaticPages_ReturnContent(string slug)
    {
        var result = await _handler.Handle(new GetContentPageQuery(slug), CancellationToken.None);
        result.IsError.Should().BeFalse();
        result.Value.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NonexistentPage_ReturnsError()
    {
        var result = await _handler.Handle(new GetContentPageQuery("nonexistent"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CONTENT_NOT_FOUND");
    }
}
