using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetEmailLogs;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetEmailLogs;

[Trait("Category", "Handlers")]
public class GetEmailLogsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetEmailLogsHandler _handler;

    public GetEmailLogsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetEmailLogsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedEmailLogs()
    {
        _sets.EmailLogs.Add(new EmailLog { LogId = 1, Type = "Verification", Recipient = "jan@test.com", Subject = "Potwierdz", Status = "sent", CreatedAt = DateTime.UtcNow });
        _sets.EmailLogs.Add(new EmailLog { LogId = 2, Type = "TwoFactorAuth", Recipient = "anna@test.com", Subject = "Kod 2FA", Status = "failed", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        _sets.EmailLogs.Add(new EmailLog { LogId = 3, Type = "PasswordReset", Recipient = "piotr@test.com", Subject = "Reset", Status = "pending", CreatedAt = DateTime.UtcNow.AddHours(-2) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetEmailLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(3);
        result.Value.Data[0].LogId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsOnlyMatching()
    {
        _sets.EmailLogs.Add(new EmailLog { LogId = 1, Type = "Verification", Recipient = "a@t.com", Subject = "S", Status = "sent", CreatedAt = DateTime.UtcNow });
        _sets.EmailLogs.Add(new EmailLog { LogId = 2, Type = "Verification", Recipient = "b@t.com", Subject = "S", Status = "failed", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetEmailLogsQuery(new PaginationParams(1, 20), Status: "failed"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Status.Should().Be("failed");
    }

    [Fact]
    public async Task Handle_WithTypeFilter_ReturnsOnlyMatching()
    {
        _sets.EmailLogs.Add(new EmailLog { LogId = 1, Type = "Verification", Recipient = "a@t.com", Subject = "S", Status = "sent", CreatedAt = DateTime.UtcNow });
        _sets.EmailLogs.Add(new EmailLog { LogId = 2, Type = "TwoFactorAuth", Recipient = "b@t.com", Subject = "S", Status = "sent", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetEmailLogsQuery(new PaginationParams(1, 20), Type: "TwoFactorAuth"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Type.Should().Be("TwoFactorAuth");
    }

    [Fact]
    public async Task Handle_WithBothFilters_AppliesBoth()
    {
        _sets.EmailLogs.Add(new EmailLog { LogId = 1, Type = "Verification", Recipient = "a@t.com", Subject = "S", Status = "sent", CreatedAt = DateTime.UtcNow });
        _sets.EmailLogs.Add(new EmailLog { LogId = 2, Type = "Verification", Recipient = "b@t.com", Subject = "S", Status = "failed", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        _sets.EmailLogs.Add(new EmailLog { LogId = 3, Type = "PasswordReset", Recipient = "c@t.com", Subject = "S", Status = "failed", CreatedAt = DateTime.UtcNow.AddHours(-2) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetEmailLogsQuery(new PaginationParams(1, 20), Status: "failed", Type: "Verification"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].LogId.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetEmailLogsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetEmailLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
