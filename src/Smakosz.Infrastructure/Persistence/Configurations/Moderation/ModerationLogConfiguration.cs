using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Moderation;

public class ModerationLogConfiguration : IEntityTypeConfiguration<ModerationLog>
{
    public void Configure(EntityTypeBuilder<ModerationLog> builder)
    {
        builder.ToTable("moderation_logs", "system");

        builder.HasKey(x => x.LogId);

        builder.Property(x => x.LogId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EntityType)
            .HasConversion(new SnakeCaseEnumConverter<ModerationEntityType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.Actor)
            .HasConversion(new SnakeCaseEnumConverter<ModerationActor>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Verdict)
            .HasConversion(new ModerationVerdictConverter())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AiScores)
            .HasColumnType("jsonb");

        builder.HasOne(x => x.ProcessedByUser)
            .WithMany()
            .HasForeignKey(x => x.ProcessedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasIndex(x => new { x.ProcessedBy, x.CreatedAt })
            .HasFilter("processed_by IS NOT NULL")
            .IsDescending(false, true);
    }
}
