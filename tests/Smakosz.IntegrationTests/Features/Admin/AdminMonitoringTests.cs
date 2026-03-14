using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Admin;

public class AdminMonitoringTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateAdminUser(99, hash));
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash));
            db.SiteStats.Add(SeedHelpers.CreateSiteStats());

            db.AuditLogs.AddRange(
                new AuditLog { TableName = "config", RecordId = 1, Operation = AuditOperation.Insert, ChangedBy = "admin (99)", ChangedAt = DateTime.UtcNow },
                new AuditLog { TableName = "cities", RecordId = 1, Operation = AuditOperation.Update, ChangedBy = "admin (99)", ChangedAt = DateTime.UtcNow.AddHours(-1) });

            db.SecurityLogs.AddRange(
                new SecurityLog { EventType = SecurityEventType.FailedLogin, Email = "hacker@test.com", IpAddress = "192.168.1.100", CreatedAt = DateTime.UtcNow },
                new SecurityLog { EventType = SecurityEventType.PasswordChanged, Email = "jan@smakosz.test", UserId = 1, CreatedAt = DateTime.UtcNow.AddHours(-1) });

            db.SystemNodes.AddRange(
                new SystemNode { NodeId = "api-main", NodeType = NodeType.Api, Role = NodeRole.Dispatcher, Status = "online", LastHeartbeat = DateTime.UtcNow },
                new SystemNode { NodeId = "gpu-worker-1", NodeType = NodeType.Gpu, Role = NodeRole.Worker, Status = "offline", LastHeartbeat = DateTime.UtcNow.AddDays(-1) });

            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetAuditLogs_AsAdmin_Returns200()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAuditLogs_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.GetAsync("/api/admin/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuditLogs_WithTableFilter_FiltersResults()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/audit-logs?tableName=config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<Application.Common.Models.PagedResult<Application.Features.Admin.Dtos.AuditLogDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Should().AllSatisfy(x => x.TableName.Should().Be("config"));
    }

    [Fact]
    public async Task GetSecurityLogs_AsAdmin_Returns200()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/security-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSecurityLogs_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.GetAsync("/api/admin/security-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSecurityLogs_WithEventTypeFilter_FiltersResults()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/security-logs?eventType=FailedLogin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<Application.Common.Models.PagedResult<Application.Features.Admin.Dtos.SecurityLogDto>>(response);
        result.Should().NotBeNull();
        result!.Data.Should().AllSatisfy(x => x.EventType.Should().Be("FailedLogin"));
    }

    [Fact]
    public async Task GetSystemNodes_AsAdmin_Returns200()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/nodes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSystemNodes_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.GetAsync("/api/admin/nodes");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
