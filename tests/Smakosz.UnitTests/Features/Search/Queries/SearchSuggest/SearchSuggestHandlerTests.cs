using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Search.Queries.SearchSuggest;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Search.Queries.SearchSuggest;

[Trait("Category", "Handlers")]
public class SearchSuggestHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IPublicConfigProvider _config;
    private readonly SearchSuggestHandler _handler;

    public SearchSuggestHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _config = Substitute.For<IPublicConfigProvider>();
        _config.GetDoubleAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<double>(1)));
        _handler = new SearchSuggestHandler(_db, _config);
    }

    // FromSqlInterpolated requires a real database connection so similarity-matching tests live in IntegrationTests; these unit tests cover the input validation paths that short-circuit before any query runs.

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
    public async Task SingleCharacterAfterTrim_ReturnsEmpty()
    {
        var result = await _handler.Handle(new SearchSuggestQuery("  b  "), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
