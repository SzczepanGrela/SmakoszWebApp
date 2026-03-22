using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateTag;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateTag;

[Trait("Category", "Handlers")]
public class UpdateTagHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateTagHandler _handler;

    public UpdateTagHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new UpdateTagHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesTag()
    {
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Stary", Category = "Typ", TargetEntity = TagTargetEntity.Both });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateTagCommand(1, "Nowy", null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Tags[0].TagName.Should().Be("Nowy");
        _sets.Tags[0].Category.Should().Be("Typ");
        _sets.AuditLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_TagNotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new UpdateTagCommand(999, "X", null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TAG_NOT_FOUND");
    }
}
