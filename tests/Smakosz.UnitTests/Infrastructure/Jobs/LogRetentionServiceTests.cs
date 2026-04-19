using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Orchestrator.Jobs;

namespace Smakosz.UnitTests.Infrastructure.Jobs;

[Trait("Category", "Handlers")]
public class LogRetentionServiceTests : IDisposable
{
    private readonly SmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LogRetentionService> _logger;
    private readonly LogRetentionService _service;

    private static readonly DateTime Now = new(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

    public LogRetentionServiceTests()
    {
        var options = new DbContextOptionsBuilder<SmakoszDbContext>()
            .UseInMemoryDatabase($"LogRetention_{Guid.NewGuid():N}")
            .Options;
        _db = new SmakoszDbContext(options);
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(Now);
        _logger = Substitute.For<ILogger<LogRetentionService>>();
        _service = new LogRetentionService(_db, _clock, _logger);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CleanupAsync_NoOldEntries_DeletesNothing()
    {
        _db.SecurityLogs.Add(new SecurityLog { EventType = SecurityEventType.FailedLogin, CreatedAt = Now.AddDays(-1) });
        _db.AuditLogs.Add(new AuditLog { TableName = "users", RecordId = 1, Operation = AuditOperation.Update, ChangedBy = "admin", ChangedAt = Now.AddDays(-1) });
        _db.EmailLogs.Add(new EmailLog { Recipient = "a@b.com", Subject = "x", Status = "sent", CreatedAt = Now.AddDays(-1) });
        _db.ModerationLogs.Add(new ModerationLog { EntityType = ModerationEntityType.Review, EntityId = 1, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = Now.AddDays(-1) });
        _db.AiLogs.Add(new AiLog { ModelType = "text_moderation", CreatedAt = Now.AddDays(-1) });
        await _db.SaveChangesAsync();

        await _service.CleanupAsync(CancellationToken.None);

        (await _db.SecurityLogs.CountAsync()).Should().Be(1);
        (await _db.AuditLogs.CountAsync()).Should().Be(1);
        (await _db.EmailLogs.CountAsync()).Should().Be(1);
        (await _db.ModerationLogs.CountAsync()).Should().Be(1);
        (await _db.AiLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CleanupAsync_OldEntriesAcrossAllTables_DeletedWithDefaults()
    {
        _db.SecurityLogs.Add(new SecurityLog { EventType = SecurityEventType.FailedLogin, Email = "old", CreatedAt = Now.AddDays(-91) });
        _db.SecurityLogs.Add(new SecurityLog { EventType = SecurityEventType.FailedLogin, Email = "new", CreatedAt = Now.AddDays(-30) });

        _db.AuditLogs.Add(new AuditLog { TableName = "users", RecordId = 1, Operation = AuditOperation.Update, ChangedBy = "old", ChangedAt = Now.AddDays(-366) });
        _db.AuditLogs.Add(new AuditLog { TableName = "users", RecordId = 2, Operation = AuditOperation.Update, ChangedBy = "new", ChangedAt = Now.AddDays(-100) });

        _db.EmailLogs.Add(new EmailLog { Recipient = "old@b.com", Subject = "x", Status = "sent", CreatedAt = Now.AddDays(-61) });
        _db.EmailLogs.Add(new EmailLog { Recipient = "new@b.com", Subject = "x", Status = "sent", CreatedAt = Now.AddDays(-10) });

        _db.ModerationLogs.Add(new ModerationLog { EntityType = ModerationEntityType.Review, EntityId = 1, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = Now.AddDays(-181) });
        _db.ModerationLogs.Add(new ModerationLog { EntityType = ModerationEntityType.Review, EntityId = 2, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = Now.AddDays(-30) });

        _db.AiLogs.Add(new AiLog { ModelType = "text_moderation", EntityType = "old", CreatedAt = Now.AddDays(-31) });
        _db.AiLogs.Add(new AiLog { ModelType = "text_moderation", EntityType = "new", CreatedAt = Now.AddDays(-5) });

        await _db.SaveChangesAsync();

        await _service.CleanupAsync(CancellationToken.None);

        (await _db.SecurityLogs.SingleAsync()).Email.Should().Be("new");
        (await _db.AuditLogs.SingleAsync()).ChangedBy.Should().Be("new");
        (await _db.EmailLogs.SingleAsync()).Recipient.Should().Be("new@b.com");
        (await _db.ModerationLogs.SingleAsync()).EntityId.Should().Be(2);
        (await _db.AiLogs.SingleAsync()).EntityType.Should().Be("new");
    }

    [Fact]
    public async Task CleanupAsync_CustomAiLogsTtl_OverridesDefault()
    {
        _db.SystemConfigs.Add(new SystemConfig { Key = "retention.ai_logs_days", Value = "7" });
        _db.AiLogs.Add(new AiLog { ModelType = "text_moderation", EntityType = "old", CreatedAt = Now.AddDays(-10) });
        _db.AiLogs.Add(new AiLog { ModelType = "text_moderation", EntityType = "new", CreatedAt = Now.AddDays(-5) });
        await _db.SaveChangesAsync();

        await _service.CleanupAsync(CancellationToken.None);

        (await _db.AiLogs.SingleAsync()).EntityType.Should().Be("new");
    }

    [Fact]
    public async Task CleanupAsync_AllFiveTablesProcessed_InSingleRun()
    {
        _db.SecurityLogs.Add(new SecurityLog { EventType = SecurityEventType.FailedLogin, CreatedAt = Now.AddDays(-100) });
        _db.AuditLogs.Add(new AuditLog { TableName = "users", RecordId = 1, Operation = AuditOperation.Update, ChangedBy = "admin", ChangedAt = Now.AddDays(-400) });
        _db.EmailLogs.Add(new EmailLog { Recipient = "a@b.com", Subject = "x", Status = "sent", CreatedAt = Now.AddDays(-70) });
        _db.ModerationLogs.Add(new ModerationLog { EntityType = ModerationEntityType.Review, EntityId = 1, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = Now.AddDays(-200) });
        _db.AiLogs.Add(new AiLog { ModelType = "text_moderation", CreatedAt = Now.AddDays(-40) });
        await _db.SaveChangesAsync();

        await _service.CleanupAsync(CancellationToken.None);

        (await _db.SecurityLogs.CountAsync()).Should().Be(0);
        (await _db.AuditLogs.CountAsync()).Should().Be(0);
        (await _db.EmailLogs.CountAsync()).Should().Be(0);
        (await _db.ModerationLogs.CountAsync()).Should().Be(0);
        (await _db.AiLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CleanupAsync_AuditLogUsesChangedAt_NotCreatedAt()
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "users",
            RecordId = 1,
            Operation = AuditOperation.Update,
            ChangedBy = "admin",
            ChangedAt = Now.AddDays(-400)
        });
        await _db.SaveChangesAsync();

        await _service.CleanupAsync(CancellationToken.None);

        (await _db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CleanupAsync_FailureInOneTable_LogsWarningAndContinues()
    {
        var throwingContext = Substitute.For<ISmakoszDbContext>();
        throwingContext.SecurityLogs.Throws(new InvalidOperationException("simulated db failure"));
        throwingContext.AuditLogs.Returns(_db.AuditLogs);
        throwingContext.EmailLogs.Returns(_db.EmailLogs);
        throwingContext.ModerationLogs.Returns(_db.ModerationLogs);
        throwingContext.AiLogs.Returns(_db.AiLogs);
        throwingContext.SystemConfigs.Returns(_db.SystemConfigs);
        throwingContext.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(ci => _db.SaveChangesAsync(ci.Arg<CancellationToken>()));

        _db.AiLogs.Add(new AiLog { ModelType = "text_moderation", CreatedAt = Now.AddDays(-31) });
        await _db.SaveChangesAsync();

        var service = new LogRetentionService(throwingContext, _clock, _logger);

        await service.CleanupAsync(CancellationToken.None);

        (await _db.AiLogs.CountAsync()).Should().Be(0);
        _logger.Received().Log(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
