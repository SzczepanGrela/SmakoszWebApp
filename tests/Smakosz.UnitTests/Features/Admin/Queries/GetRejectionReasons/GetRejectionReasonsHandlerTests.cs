using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetRejectionReasons;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetRejectionReasons;

[Trait("Category", "Handlers")]
public class GetRejectionReasonsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetRejectionReasonsHandler _handler;

    public GetRejectionReasonsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(role: "Moderator");
        _handler = new GetRejectionReasonsHandler(_db, _currentUser);
    }

    private void SeedStandard()
    {
        _sets.RejectionReasons.AddRange(new[]
        {
            new RejectionReason { ReasonCode = "text_spam", Category = RejectionReasonCategory.Text, AdminLabel = "Spam", UserMessageTemplate = "Spam", IsActive = true },
            new RejectionReason { ReasonCode = "text_offtopic", Category = RejectionReasonCategory.Text, AdminLabel = "Off-topic", UserMessageTemplate = "Off", IsActive = true },
            new RejectionReason { ReasonCode = "photo_nudity", Category = RejectionReasonCategory.Photo, AdminLabel = "Nagość", UserMessageTemplate = "N", IsActive = true },
            new RejectionReason { ReasonCode = "text_inactive", Category = RejectionReasonCategory.Text, AdminLabel = "Nieaktywne", UserMessageTemplate = "X", IsActive = false }
        });
        DbContextMockFactory.Refresh(_db, _sets);
    }

    [Fact]
    public async Task Handle_NotAdminOrModerator_ReturnsForbidden()
    {
        var userService = MockExtensions.CreateAuthenticatedUser(role: "User");
        var handler = new GetRejectionReasonsHandler(_db, userService);

        var result = await handler.Handle(
            new GetRejectionReasonsQuery(new PaginationParams()),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_Default_ReturnsOnlyActive()
    {
        SeedStandard();

        var result = await _handler.Handle(
            new GetRejectionReasonsQuery(new PaginationParams(1, 100)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(3);
        result.Value.Data.Should().OnlyContain(r => r.IsActive);
    }

    [Fact]
    public async Task Handle_IncludeInactive_ReturnsAll()
    {
        SeedStandard();

        var result = await _handler.Handle(
            new GetRejectionReasonsQuery(new PaginationParams(1, 100), IncludeInactive: true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_CategoryFilter_NarrowsResults()
    {
        SeedStandard();

        var result = await _handler.Handle(
            new GetRejectionReasonsQuery(new PaginationParams(1, 100), Category: "Text"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().OnlyContain(r => r.Category == "Text");
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_InvalidCategory_ReturnsValidationError()
    {
        var result = await _handler.Handle(
            new GetRejectionReasonsQuery(new PaginationParams(), Category: "Video"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_INVALID_CATEGORY");
    }

    [Fact]
    public async Task Handle_SortsByCategoryThenLabel()
    {
        SeedStandard();

        var result = await _handler.Handle(
            new GetRejectionReasonsQuery(new PaginationParams(1, 100)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var ordered = result.Value.Data;
        ordered[0].Category.Should().Be(nameof(RejectionReasonCategory.Photo));
        ordered[1].Category.Should().Be(nameof(RejectionReasonCategory.Text));
        ordered[2].Category.Should().Be(nameof(RejectionReasonCategory.Text));
    }
}
