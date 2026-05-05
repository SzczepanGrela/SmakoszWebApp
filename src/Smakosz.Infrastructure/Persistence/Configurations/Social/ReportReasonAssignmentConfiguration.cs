using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class ReportReasonAssignmentConfiguration : IEntityTypeConfiguration<ReportReasonAssignment>
{
    public void Configure(EntityTypeBuilder<ReportReasonAssignment> builder)
    {
        builder.HasKey(x => new { x.ReportId, x.ReasonCode });

        builder.HasQueryFilter(x => !x.Report.Reporter.IsDeleted);

        builder.Property(x => x.ReasonCode)
            .HasMaxLength(50);

        builder.HasOne(x => x.Report)
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReasonDefinition)
            .WithMany()
            .HasForeignKey(x => x.ReasonCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ReasonCode);
    }
}
