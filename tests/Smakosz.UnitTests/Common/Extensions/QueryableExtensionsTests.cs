using FluentAssertions;
using MockQueryable.NSubstitute;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Entities;

namespace Smakosz.UnitTests.Common.Extensions;

[Trait("Category", "Extensions")]
public class QueryableExtensionsTests
{
    private static IQueryable<City> BuildMockQueryable(IEnumerable<City> items)
    {
        return items.AsQueryable().BuildMockDbSet();
    }

    [Fact]
    public async Task ToPagedResultAsync_FirstPage_ReturnsCorrectData()
    {
        var items = Enumerable.Range(1, 25).Select(i => new City { CityId = i, CityName = $"City {i}" }).ToList();
        var queryable = BuildMockQueryable(items);
        var pagination = new PaginationParams(Page: 1, PageSize: 10);

        var result = await queryable.ToPagedResultAsync(pagination);

        result.Data.Should().HaveCount(10);
        result.Data.First().CityId.Should().Be(1);
        result.Pagination.Page.Should().Be(1);
        result.Pagination.PageSize.Should().Be(10);
        result.Pagination.TotalCount.Should().Be(25);
        result.Pagination.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task ToPagedResultAsync_LastPage_ReturnsPartialData()
    {
        var items = Enumerable.Range(1, 25).Select(i => new City { CityId = i, CityName = $"City {i}" }).ToList();
        var queryable = BuildMockQueryable(items);
        var pagination = new PaginationParams(Page: 3, PageSize: 10);

        var result = await queryable.ToPagedResultAsync(pagination);

        result.Data.Should().HaveCount(5);
        result.Data.First().CityId.Should().Be(21);
        result.Pagination.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task ToPagedResultAsync_EmptySource_ReturnsEmptyResult()
    {
        var queryable = BuildMockQueryable(new List<City>());
        var pagination = new PaginationParams(Page: 1, PageSize: 10);

        var result = await queryable.ToPagedResultAsync(pagination);

        result.Data.Should().BeEmpty();
        result.Pagination.TotalCount.Should().Be(0);
        result.Pagination.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task ToPagedResultAsync_ExactFit_ReturnsOnePage()
    {
        var items = Enumerable.Range(1, 10).Select(i => new City { CityId = i, CityName = $"City {i}" }).ToList();
        var queryable = BuildMockQueryable(items);
        var pagination = new PaginationParams(Page: 1, PageSize: 10);

        var result = await queryable.ToPagedResultAsync(pagination);

        result.Data.Should().HaveCount(10);
        result.Pagination.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task ToPagedResultAsync_SingleItem_ReturnsOnePage()
    {
        var queryable = BuildMockQueryable(new List<City> { new() { CityId = 42, CityName = "Answer" } });
        var pagination = new PaginationParams(Page: 1, PageSize: 20);

        var result = await queryable.ToPagedResultAsync(pagination);

        result.Data.Should().ContainSingle().Which.CityId.Should().Be(42);
        result.Pagination.TotalCount.Should().Be(1);
        result.Pagination.TotalPages.Should().Be(1);
    }
}
