using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteForbiddenWord;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteForbiddenWord;

[Trait("Category", "Handlers")]
public class DeleteForbiddenWordHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly DeleteForbiddenWordHandler _handler;

    public DeleteForbiddenWordHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new DeleteForbiddenWordHandler(_db, _currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_DeletesWord()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "bad", Category = ForbiddenWordCategory.Profanity });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteForbiddenWordCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteForbiddenWordCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new DeleteForbiddenWordHandler(_db, nonAdmin, _forbiddenWords);

        var result = await handler.Handle(new DeleteForbiddenWordCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_CreatesAuditLog()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "bad", Category = ForbiddenWordCategory.Profanity });
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new DeleteForbiddenWordCommand(1), CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Delete);
    }

    [Fact]
    public async Task Handle_InvalidatesCache()
    {
        _sets.ForbiddenWords.Add(new ForbiddenWord { WordId = 1, Word = "bad", Category = ForbiddenWordCategory.Profanity });
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new DeleteForbiddenWordCommand(1), CancellationToken.None);

        _forbiddenWords.Received(1).InvalidateCache();
    }
}
