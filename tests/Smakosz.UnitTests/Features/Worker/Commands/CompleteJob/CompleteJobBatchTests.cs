using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Worker.Commands.CompleteJob;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Microsoft.Extensions.Logging;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Worker.Commands.CompleteJob;

[Trait("Category", "Handlers")]
public class CompleteJobBatchTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IDateTimeProvider _clock;
    private readonly CompleteJobHandler _handler;
    private static readonly DateTime Now = new(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc);

    public CompleteJobBatchTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(Now);
        var mediator = Substitute.For<MediatR.IMediator>();
        var logger = Substitute.For<ILogger<CompleteJobHandler>>();
        _handler = new CompleteJobHandler(_db, _clock, mediator, logger);
    }

    [Fact]
    public async Task HandleTextBatch_ReviewApproved_SetsApprovedStatus()
    {
        var review = new ReviewBuilder().WithDish(new DishBuilder().WithId(1).Build()).Build();
        review.ReviewId = 10;
        review.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Reviews.Add(review);

        var job = CreateBatchJob("text_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = new
        {
            results = new[]
            {
                new { entity_type = "review", entity_id = 10, toxicity_score = 0.05, verdict = "approved", model_version = "v1" }
            }
        };

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, JsonSerializer.Serialize(batchResult), 100),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        _sets.ModerationLogs.Should().HaveCount(1);
        _sets.AiLogs.Should().HaveCount(1);

        _sets.ModerationResults.Should().HaveCount(1);
        var moderationResult = _sets.ModerationResults.Single();
        moderationResult.Scores.Should().Contain("\"toxicity_score\"");
        moderationResult.Scores.Should().NotContain("nsfw_score");
    }

    [Fact]
    public async Task HandleTextBatch_ReviewRejected_SetsRejectedStatus()
    {
        var review = new ReviewBuilder().WithDish(new DishBuilder().WithId(1).Build()).Build();
        review.ReviewId = 20;
        review.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Reviews.Add(review);

        var job = CreateBatchJob("text_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = new
        {
            results = new[]
            {
                new { entity_type = "review", entity_id = 20, toxicity_score = 0.95, verdict = "rejected", model_version = "v1" }
            }
        };

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, JsonSerializer.Serialize(batchResult), 100),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.Rejected);
    }

    [Fact]
    public async Task HandleTextBatch_DishRejected_SetsRejectedStatus()
    {
        var dish = new DishBuilder().WithId(5).Build();
        dish.DishId = 5;
        dish.DishName = "Current Name";
        dish.Description = "Current desc";
        dish.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Dishes.Add(dish);

        var job = CreateBatchJob("text_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = JsonSerializer.Serialize(new
        {
            results = new object[]
            {
                new { entity_type = "dish", entity_id = 5, toxicity_score = 0.9, verdict = "rejected", model_version = "v1" }
            }
        });

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, batchResult, 200),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.ModerationStatus.Should().Be(ContentModerationStatus.Rejected);
        dish.DishName.Should().Be("Current Name");
        dish.Description.Should().Be("Current desc");
    }

    [Fact]
    public async Task HandleTextBatch_DishApproved_KeepsCurrentValues()
    {
        var dish = new DishBuilder().WithId(6).Build();
        dish.DishId = 6;
        dish.DishName = "New Name";
        dish.Description = "New desc";
        dish.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Dishes.Add(dish);

        var job = CreateBatchJob("text_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = JsonSerializer.Serialize(new
        {
            results = new object[]
            {
                new { entity_type = "dish", entity_id = 6, toxicity_score = 0.02, verdict = "approved", model_version = "v1" }
            }
        });

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, batchResult, 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        dish.DishName.Should().Be("New Name");
    }

    [Fact]
    public async Task HandleTextBatch_RestaurantApproved_SetsApprovedStatus()
    {
        var restaurant = new RestaurantBuilder().WithId(7).Build();
        restaurant.RestaurantId = 7;
        restaurant.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Restaurants.Add(restaurant);

        var job = CreateBatchJob("text_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = JsonSerializer.Serialize(new
        {
            results = new object[]
            {
                new { entity_type = "restaurant", entity_id = 7, toxicity_score = 0.01, verdict = "approved", model_version = "v1" }
            }
        });

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, batchResult, 30),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        restaurant.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
    }

    [Fact]
    public async Task HandleTextBatch_MultipleEntities_ProcessesAll()
    {
        var review = new ReviewBuilder().WithDish(new DishBuilder().WithId(1).Build()).Build();
        review.ReviewId = 30;
        review.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Reviews.Add(review);

        var dish = new DishBuilder().WithId(8).Build();
        dish.DishId = 8;
        dish.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Dishes.Add(dish);

        var job = CreateBatchJob("text_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = JsonSerializer.Serialize(new
        {
            results = new object[]
            {
                new { entity_type = "review", entity_id = 30, toxicity_score = 0.1, verdict = "approved", model_version = "v1" },
                new { entity_type = "dish", entity_id = 8, toxicity_score = 0.05, verdict = "approved", model_version = "v1" }
            }
        });

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, batchResult, 150),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        dish.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        _sets.ModerationLogs.Should().HaveCount(2);
        _sets.AiLogs.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleImageBatch_AssetApproved_SetsApprovedStatus()
    {
        var asset = new MediaAsset
        {
            AssetId = 100,
            Url = "https://cdn.example.com/img.jpg",
            ModerationStatus = ContentModerationStatus.Processing,
            EntityType = MediaEntityType.Dish
        };
        _sets.MediaAssets.Add(asset);

        var job = CreateBatchJob("image_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = JsonSerializer.Serialize(new
        {
            results = new object[]
            {
                new { entity_type = "media_asset", entity_id = 100, nsfw_score = 0.05, relevance_score = 0.9, verdict = "approved", model_version = "nsfw-v1_clip-v1" }
            }
        });

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, batchResult, 300),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        _sets.ModerationLogs.Should().HaveCount(1);
        _sets.AiLogs.Should().HaveCount(1);

        _sets.ModerationResults.Should().HaveCount(1);
        var moderationResult = _sets.ModerationResults.Single();
        moderationResult.Scores.Should().Contain("\"nsfw_score\"");
        moderationResult.Scores.Should().Contain("\"relevance_score\"");
        moderationResult.Scores.Should().NotContain("toxicity_score");
    }

    [Fact]
    public async Task HandleImageBatch_AssetRejected_SetsRejectedStatus()
    {
        var asset = new MediaAsset
        {
            AssetId = 101,
            Url = "https://cdn.example.com/nsfw.jpg",
            ModerationStatus = ContentModerationStatus.Processing,
            EntityType = MediaEntityType.Dish
        };
        _sets.MediaAssets.Add(asset);

        var job = CreateBatchJob("image_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = JsonSerializer.Serialize(new
        {
            results = new object[]
            {
                new { entity_type = "media_asset", entity_id = 101, nsfw_score = 0.95, relevance_score = 0.8, verdict = "rejected", model_version = "nsfw-v1_clip-v1" }
            }
        });

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, batchResult, 250),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.ModerationStatus.Should().Be(ContentModerationStatus.Rejected);
    }

    [Fact]
    public async Task HandleTextBatch_ReviewNeedsReview_SetsNeedsReviewStatus()
    {
        var review = new ReviewBuilder().WithDish(new DishBuilder().WithId(1).Build()).Build();
        review.ReviewId = 40;
        review.ModerationStatus = ContentModerationStatus.Processing;
        _sets.Reviews.Add(review);

        var job = CreateBatchJob("text_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = new
        {
            results = new[]
            {
                new { entity_type = "review", entity_id = 40, toxicity_score = 0.55, verdict = "needs_review", model_version = "v1" }
            }
        };

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, JsonSerializer.Serialize(batchResult), 100),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.NeedsReview);
    }

    [Fact]
    public async Task HandleImageBatch_AssetNeedsReview_SetsNeedsReviewAndKeepsTicketOpen()
    {
        var asset = new MediaAsset
        {
            AssetId = 102,
            Url = "https://cdn.example.com/uncertain.jpg",
            ModerationStatus = ContentModerationStatus.Processing,
            EntityType = MediaEntityType.Dish
        };
        _sets.MediaAssets.Add(asset);

        var ticket = new SystemTicket
        {
            TicketId = 10,
            TicketType = TicketType.Photo,
            ReferenceId = 102,
            Status = TicketStatus.Open,
            Priority = 3
        };
        _sets.SystemTickets.Add(ticket);

        var job = CreateBatchJob("image_moderation_batch");
        _sets.SystemJobs.Add(job);
        DbContextMockFactory.Refresh(_db, _sets);

        var batchResult = JsonSerializer.Serialize(new
        {
            results = new object[]
            {
                new { entity_type = "media_asset", entity_id = 102, nsfw_score = 0.45, relevance_score = 0.6, verdict = "needs_review", model_version = "nsfw-v1_clip-v1" }
            }
        });

        var result = await _handler.Handle(
            new CompleteJobCommand(job.JobId, batchResult, 200),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.ModerationStatus.Should().Be(ContentModerationStatus.NeedsReview);
        ticket.Status.Should().Be(TicketStatus.Open);
    }

    private static SystemJob CreateBatchJob(string type) => new()
    {
        JobId = 1,
        Type = type,
        Status = JobStatus.Processing,
        Priority = 5,
        Payload = "{}",
        StartedAt = Now.AddMinutes(-1)
    };
}
