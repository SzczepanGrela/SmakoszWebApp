using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class SystemJobConfiguration : IEntityTypeConfiguration<SystemJob>
{
    public void Configure(EntityTypeBuilder<SystemJob> builder)
    {
        builder.ToTable("jobs", "system");

        builder.HasKey(x => x.JobId);

        builder.Property(x => x.Type)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(new UpperCaseEnumConverter<JobStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.Payload)
            .HasColumnType("jsonb");

        builder.Property(x => x.Result)
            .HasColumnType("jsonb");

        builder.Property(x => x.EntityId)
            .HasMaxLength(50);

        builder.Property(x => x.EntityType)
            .HasMaxLength(30);

        builder.Property(x => x.WorkerNode)
            .HasMaxLength(50);

        builder.Property(x => x.MaxAttempts)
            .HasDefaultValue(3);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Worker)
            .WithMany()
            .HasForeignKey(x => x.WorkerNode)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => new { x.Status, x.Priority, x.CreatedAt })
            .IsDescending(false, true, false);

        builder.HasIndex(x => x.WorkerNode);

        builder.HasIndex(x => new { x.EntityType, x.EntityId });

        builder.HasIndex(x => new { x.Status, x.Priority, x.CreatedAt })
            .HasFilter("status = 'PENDING'")
            .HasDatabaseName("ix_jobs_pull_queue")
            .IsDescending(false, true, false);

        builder.HasIndex(x => new { x.Status, x.StartedAt })
            .HasFilter("status = 'PROCESSING'")
            .HasDatabaseName("ix_jobs_stuck_monitor");
    }
}
