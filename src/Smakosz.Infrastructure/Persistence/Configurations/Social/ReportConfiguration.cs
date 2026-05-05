using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.HasKey(x => x.ReportId);

        builder.HasQueryFilter(x => !x.Reporter.IsDeleted);

        builder.Property(x => x.EntityType)
            .HasConversion(new SnakeCaseEnumConverter<ReportEntityType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .HasConversion(new SnakeCaseEnumConverter<ReportStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.Reporter)
            .WithMany()
            .HasForeignKey(x => x.ReporterId);

        builder.HasOne(x => x.ResolvedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ResolvedByAdminId);

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
