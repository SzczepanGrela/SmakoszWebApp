using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetHeroImages;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetHeroImages;

[Trait("Category", "Handlers")]
public class GetHeroImagesHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetHeroImagesHandler _handler;

    public GetHeroImagesHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetHeroImagesHandler(_db);
    }

    [Fact]
    public async Task Handle_ReturnsHeroImages()
    {
        _sets.MediaAssets.Add(new MediaAsset
        {
            AssetId = 1, PublicId = Guid.NewGuid(), EntityType = MediaEntityType.Hero,
            Url = "http://hero.jpg", Blurhash = "abc", CreditText = "Photo by X",
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetHeroImagesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].Url.Should().Be("http://hero.jpg");
        result.Value[0].CreditText.Should().Be("Photo by X");
    }

    [Fact]
    public async Task Handle_Empty_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetHeroImagesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
