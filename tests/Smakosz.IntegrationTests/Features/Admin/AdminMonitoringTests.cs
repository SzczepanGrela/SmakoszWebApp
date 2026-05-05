using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.IntegrationTests.Infrastructure;
using System.Text;
using System.Text.Json;

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

    [Fact]
    public async Task GetSystemNodes_ResponseIncludesStaleThresholdDays()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/nodes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("staleThresholdDays");
        body.Should().Contain("nodes");
    }

    [Fact]
    public async Task AddNode_AsAdmin_ReturnsNoContent_AndPersists()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.SystemNodes.Add(new SystemNode
            {
                NodeId = "rbpi-test-gw",
                NodeType = NodeType.RbpiGateway,
                Status = "online",
                LastHeartbeat = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");
        var payload = JsonSerializer.Serialize(new
        {
            NodeId = "test-gpu-add",
            NodeType = "gpu",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            WolGatewayId = "rbpi-test-gw"
        });

        var response = await client.PostAsync("/api/admin/nodes",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        var added = await db.SystemNodes.FirstOrDefaultAsync(n => n.NodeId == "test-gpu-add");
        added.Should().NotBeNull();
        added!.NodeType.Should().Be(NodeType.Gpu);
    }

    [Fact]
    public async Task AddNode_DuplicateNodeId_Returns409()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");
        var payload = JsonSerializer.Serialize(new
        {
            NodeId = "api-main",
            NodeType = "api",
            MacAddress = (string?)null,
            WolGatewayId = (string?)null
        });

        var response = await client.PostAsync("/api/admin/nodes",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddNode_GpuWithoutMac_Returns422()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");
        var payload = JsonSerializer.Serialize(new
        {
            NodeId = "test-gpu-incomplete",
            NodeType = "gpu",
            MacAddress = (string?)null,
            WolGatewayId = "api-main"
        });

        var response = await client.PostAsync("/api/admin/nodes",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteNode_RecentHeartbeat_Returns409()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.DeleteAsync("/api/admin/nodes/api-main");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteNode_OldHeartbeat_ReturnsNoContent()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.SystemNodes.Add(new SystemNode
            {
                NodeId = "stale-node-it",
                NodeType = NodeType.Api,
                Status = "offline",
                LastHeartbeat = DateTime.UtcNow.AddDays(-30)
            });
            await db.SaveChangesAsync();
        });

        using var client = Factory.CreateAdminClient(99, "administrator");
        var response = await client.DeleteAsync("/api/admin/nodes/stale-node-it");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        var deleted = await db.SystemNodes.FirstOrDefaultAsync(n => n.NodeId == "stale-node-it");
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNode_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");
        var response = await client.DeleteAsync("/api/admin/nodes/api-main");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ScheduleNcfTraining_InsertsPendingRowImmediately()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.PostAsync("/api/admin/ncf-training/schedule", null);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NoContent,
            HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        var jobs = await db.SystemJobs.Where(j => j.Type == "ncf_training").ToListAsync();
        jobs.Should().NotBeEmpty();
        jobs.Should().Contain(j => j.Status == JobStatus.Pending);
    }
}
