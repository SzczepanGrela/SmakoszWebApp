using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetForbiddenWords;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetForbiddenWords;

[Trait("Category", "Handlers")]
public class GetForbiddenWordsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetForbiddenWordsHandler _handler;

    public GetForbiddenWordsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetForbiddenWordsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedWords()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "abc", Category = ForbiddenWordCategory.Profanity });
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 2, Word = "xyz", Category = ForbiddenWordCategory.Offensive });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetForbiddenWordsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithSearch_FiltersResults()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "hello", Category = ForbiddenWordCategory.Profanity });
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 2, Word = "world", Category = ForbiddenWordCategory.Offensive });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetForbiddenWordsQuery(new PaginationParams(1, 20), "hell"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Word.Should().Be("hello");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetForbiddenWordsHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetForbiddenWordsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyData()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetForbiddenWordsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().BeEmpty();
    }
}
