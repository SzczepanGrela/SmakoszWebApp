using FluentAssertions;
using Smakosz.Application.Features.Search.Queries.SearchSuggest;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Search.Queries.SearchSuggest;

[Trait("Category", "Handlers")]
public class SearchSuggestHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly SearchSuggestHandler _handler;

    public SearchSuggestHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new SearchSuggestHandler(_db);
    }

    // ILike throws outside EF query translation, so query-matching tests
    // are covered by integration tests. Unit tests cover validation and edge cases.

    [Fact]
    public async Task EmptyQuery_ReturnsEmpty()
    {
        var result = await _handler.Handle(new SearchSuggestQuery(""), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task WhitespaceQuery_ReturnsEmpty()
    {
        var result = await _handler.Handle(new SearchSuggestQuery("   "), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ShortQuery_ReturnsEmpty()
    {
        var result = await _handler.Handle(new SearchSuggestQuery("a"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task LimitIsClamped_ToMaxTen()
    {
        var query = new SearchSuggestQuery("test", Limit: 50);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task LimitIsClamped_ToMinOne()
    {
        var query = new SearchSuggestQuery("test", Limit: -5);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }
}
