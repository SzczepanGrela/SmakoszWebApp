using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ProcessEditRequest;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.ProcessEditRequest;

[Trait("Category", "Handlers")]
public class ProcessEditRequestHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ProcessEditRequestHandler _handler;

    public ProcessEditRequestHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _handler = new ProcessEditRequestHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_Approve_SetsApprovedAndSetsReviewer()
    {
        var editRequest = new RestaurantEditRequest
        {
            RequestId = 1, RestaurantId = 1, UserId = 1,
            ChangeType = EditRequestChangeType.General,
            Payload = "{}", Status = EditRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _sets.RestaurantEditRequests.Add(editRequest);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ProcessEditRequestCommand(1, true, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        editRequest.Status.Should().Be(EditRequestStatus.Approved);
        editRequest.ReviewedBy.Should().Be(99);
    }

    [Fact]
    public async Task Handle_Reject_SetsRejectedStatus()
    {
        var editRequest = new RestaurantEditRequest
        {
            RequestId = 1, RestaurantId = 1, UserId = 1,
            ChangeType = EditRequestChangeType.General,
            Payload = "{}", Status = EditRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _sets.RestaurantEditRequests.Add(editRequest);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ProcessEditRequestCommand(1, false, "Not acceptable"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        editRequest.Status.Should().Be(EditRequestStatus.Rejected);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ProcessEditRequestHandler(_db, nonAdmin, _dateTime);

        var result = await handler.Handle(
            new ProcessEditRequestCommand(1, true, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_RequestNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new ProcessEditRequestCommand(999, true, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("EDIT_REQUEST_NOT_FOUND");
    }
}
