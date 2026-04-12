using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateForbiddenWord;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateForbiddenWord;

[Trait("Category", "Handlers")]
public class UpdateForbiddenWordHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly UpdateForbiddenWordHandler _handler;

    public UpdateForbiddenWordHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new UpdateForbiddenWordHandler(_db, _currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_UpdatesWord()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "old", Category = ForbiddenWordCategory.Profanity });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateForbiddenWordCommand(1, "new", null, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.ForbiddenWords[0].Word.Should().Be("new");
    }

    [Fact]
    public async Task Handle_SkipsNullFields()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "keep", Category = ForbiddenWordCategory.Profanity, IsRegex = false });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateForbiddenWordCommand(1, null, null, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.ForbiddenWords[0].Word.Should().Be("keep");
        _sets.ForbiddenWords[0].Category.Should().Be(ForbiddenWordCategory.Profanity);
    }

    [Fact]
    public async Task Handle_DuplicateWord_ReturnsError()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "first", Category = ForbiddenWordCategory.Profanity });
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 2, Word = "second", Category = ForbiddenWordCategory.Offensive });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateForbiddenWordCommand(1, "second", null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateForbiddenWordCommand(999, "x", null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_CreatesAuditLog()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "old", Category = ForbiddenWordCategory.Profanity });
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new UpdateForbiddenWordCommand(1, "new", null, null), CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Update);
    }

    [Fact]
    public async Task Handle_InvalidatesCache()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "old", Category = ForbiddenWordCategory.Profanity });
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new UpdateForbiddenWordCommand(1, "new", null, null), CancellationToken.None);

        _forbiddenWords.Received(1).InvalidateCache();
    }
}
