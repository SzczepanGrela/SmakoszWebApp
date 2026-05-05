using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class ReportReasonDefinitionConfiguration : IEntityTypeConfiguration<ReportReasonDefinition>
{
    public void Configure(EntityTypeBuilder<ReportReasonDefinition> builder)
    {
        builder.HasKey(x => x.ReasonCode);

        builder.Property(x => x.ReasonCode)
            .HasMaxLength(50);

        builder.Property(x => x.LabelPl)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SeverityScore)
            .HasDefaultValue(1);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
