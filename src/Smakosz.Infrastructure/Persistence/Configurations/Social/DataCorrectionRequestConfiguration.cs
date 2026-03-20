using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class DataCorrectionRequestConfiguration : IEntityTypeConfiguration<DataCorrectionRequest>
{
    public void Configure(EntityTypeBuilder<DataCorrectionRequest> builder)
    {
        builder.HasKey(x => x.RequestId);

        builder.Property(x => x.IssueType)
            .HasConversion(new SnakeCaseEnumConverter<DataCorrectionIssueType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.ProposedValue)
            .HasColumnType("jsonb");

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .HasDefaultValue("pending");

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RestaurantId)
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ix_data_correction_requests_pending");
    }
}
