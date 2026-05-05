using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Moderation;

public class ModerationResultConfiguration : IEntityTypeConfiguration<ModerationResult>
{
    public void Configure(EntityTypeBuilder<ModerationResult> builder)
    {
        builder.ToTable("moderation_results", "system");

        builder.HasKey(x => x.ResultId);

        builder.Property(x => x.ResultId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EntityType)
            .HasConversion(new SnakeCaseEnumConverter<ModerationEntityType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(new SnakeCaseEnumConverter<ContentModerationStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AiVerdict)
            .HasMaxLength(50);

        builder.Property(x => x.AiModelName)
            .HasMaxLength(200);

        builder.Property(x => x.AiModelVersion)
            .HasMaxLength(50);

        builder.Property(x => x.Scores)
            .HasColumnType("jsonb");

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.AutoApproveReason)
            .HasMaxLength(255);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .IsUnique();

        builder.HasIndex(x => new { x.Status, x.ProcessedAt });
    }
}
