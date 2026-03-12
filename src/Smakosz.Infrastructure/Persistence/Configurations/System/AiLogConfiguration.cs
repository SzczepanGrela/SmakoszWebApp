using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class AiLogConfiguration : IEntityTypeConfiguration<AiLog>
{
    public void Configure(EntityTypeBuilder<AiLog> builder)
    {
        builder.ToTable("ai_logs", "system");

        builder.HasKey(x => x.LogId);

        builder.Property(x => x.LogId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ModelType)
            .HasMaxLength(50);

        builder.Property(x => x.ModelName)
            .HasMaxLength(200);

        builder.Property(x => x.ModelVersion)
            .HasMaxLength(50);

        builder.Property(x => x.EntityType)
            .HasMaxLength(50);

        builder.Property(x => x.Scores)
            .HasColumnType("jsonb");

        builder.Property(x => x.Verdict)
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.EntityType, x.EntityId });

        builder.HasIndex(x => new { x.ModelType, x.CreatedAt })
            .IsDescending(false, true);
    }
}
