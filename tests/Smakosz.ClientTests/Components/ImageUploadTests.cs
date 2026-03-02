using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class ImageUploadTests : BunitTestBase
{
    [Fact]
    public void RendersFileInput()
    {
        var cut = RenderComponent<ImageUpload>();

        cut.Find("input[type='file']").Should().NotBeNull();
        cut.Markup.Should().Contain("JPG, PNG lub WebP, max 5 MB");
    }

    [Fact]
    public void WithCurrentImageUrl_ShowsCurrentImage()
    {
        var cut = RenderComponent<ImageUpload>(p => p
            .Add(c => c.CurrentImageUrl, "/img/current.jpg"));

        cut.Find("img[alt='Aktualne zdjecie']").GetAttribute("src")
            .Should().Be("/img/current.jpg");
    }

    [Fact]
    public void WithoutCurrentImage_NoImageShown()
    {
        var cut = RenderComponent<ImageUpload>();
        cut.FindAll("img").Should().BeEmpty();
    }
}
