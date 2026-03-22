using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetTags;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetTags;

[Trait("Category", "Handlers")]
public class GetTagsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetTagsHandler _handler;

    public GetTagsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetTagsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedTags()
    {
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Na wynos", Category = "Typ", TargetEntity = TagTargetEntity.Both });
        _sets.Tags.Add(new Tag { TagId = 2, TagName = "Sezonowe", Category = "Typ", TargetEntity = TagTargetEntity.Dish });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetTagsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_SearchFilter_FiltersResults()
    {
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Na wynos", Category = "Typ", TargetEntity = TagTargetEntity.Both });
        _sets.Tags.Add(new Tag { TagId = 2, TagName = "Sezonowe", Category = "Typ", TargetEntity = TagTargetEntity.Dish });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetTagsQuery(new PaginationParams(1, 20), "sezon"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }
}
