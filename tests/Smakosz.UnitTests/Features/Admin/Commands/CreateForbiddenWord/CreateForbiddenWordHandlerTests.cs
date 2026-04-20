using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreateForbiddenWord;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreateForbiddenWord;

[Trait("Category", "Handlers")]
public class CreateForbiddenWordHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly CreateForbiddenWordHandler _handler;

    public CreateForbiddenWordHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new CreateForbiddenWordHandler(_db, _currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_CreatesWord()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateForbiddenWordCommand("badword", ForbiddenWordCategory.Profanity, false), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.ForbiddenWords.Should().ContainSingle(w => w.Word == "badword");
    }

    [Fact]
    public async Task Handle_Duplicate_ReturnsAlreadyExists()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "existing", Category = ForbiddenWordCategory.Profanity });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateForbiddenWordCommand("EXISTING", ForbiddenWordCategory.Offensive, false), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_InvalidRegex_ReturnsError()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateForbiddenWordCommand("[invalid", ForbiddenWordCategory.Profanity, true), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_INVALID_REGEX");
    }

    [Fact]
    public async Task Handle_ValidRegex_Succeeds()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateForbiddenWordCommand(@"\btest\d+\b", ForbiddenWordCategory.Profanity, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new CreateForbiddenWordHandler(_db, nonAdmin, _forbiddenWords);

        var result = await handler.Handle(
            new CreateForbiddenWordCommand("word", ForbiddenWordCategory.Profanity, false), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_CreatesAuditLog()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(
            new CreateForbiddenWordCommand("testword", ForbiddenWordCategory.Offensive, false), CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].TableName.Should().Be("forbidden_words");
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Insert);
    }

    [Fact]
    public async Task Handle_InvalidatesCache()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(
            new CreateForbiddenWordCommand("word", ForbiddenWordCategory.Profanity, false), CancellationToken.None);

        _forbiddenWords.Received(1).InvalidateCache();
    }
}
