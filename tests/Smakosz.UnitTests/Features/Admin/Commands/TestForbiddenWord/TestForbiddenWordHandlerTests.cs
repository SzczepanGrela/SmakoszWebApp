using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.TestForbiddenWord;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.TestForbiddenWord;

[Trait("Category", "Handlers")]
public class TestForbiddenWordHandlerTests
{
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly TestForbiddenWordHandler _handler;

    public TestForbiddenWordHandlerTests()
    {
        _currentUser = MockExtensions.CreateAdminUser();
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new TestForbiddenWordHandler(_currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_Blocked_ReturnsTrue()
    {
        _forbiddenWords.ContainsAsync("bad text", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(
            new TestForbiddenWordCommand("bad text", new[] { ForbiddenWordCategory.Profanity }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Clean_ReturnsFalse()
    {
        _forbiddenWords.ContainsAsync("clean text", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(false);

        var result = await _handler.Handle(
            new TestForbiddenWordCommand("clean text", new[] { ForbiddenWordCategory.Profanity }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new TestForbiddenWordHandler(nonAdmin, _forbiddenWords);

        var result = await handler.Handle(
            new TestForbiddenWordCommand("text", new[] { ForbiddenWordCategory.Profanity }), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
