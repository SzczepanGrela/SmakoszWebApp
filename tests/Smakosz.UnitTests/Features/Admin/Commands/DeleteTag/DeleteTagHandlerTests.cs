using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteTag;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteTag;

[Trait("Category", "Handlers")]
public class DeleteTagHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteTagHandler _handler;

    public DeleteTagHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _handler = new DeleteTagHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_DeletesTag()
    {
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Test", Category = "Typ", TargetEntity = TagTargetEntity.Both });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteTagCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TagNotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(new DeleteTagCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TAG_NOT_FOUND");
    }
}
