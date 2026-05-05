using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class JobProgressConfiguration : IEntityTypeConfiguration<JobProgress>
{
    public void Configure(EntityTypeBuilder<JobProgress> builder)
    {
        builder.ToTable("job_progress", "system");

        builder.HasKey(x => x.ProgressId);

        builder.Property(x => x.ProgressId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Percentage)
            .HasComputedColumnSql(
                "CASE WHEN total_steps > 0 THEN (current_step::double precision / total_steps) * 100 ELSE 0 END",
                stored: true);

        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.Job)
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.JobId, x.CreatedAt })
            .IsDescending(false, true);
    }
}
