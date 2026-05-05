using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class RejectionReasonConfiguration : IEntityTypeConfiguration<RejectionReason>
{
    public void Configure(EntityTypeBuilder<RejectionReason> builder)
    {
        builder.HasKey(x => x.ReasonCode);

        builder.Property(x => x.ReasonCode)
            .HasMaxLength(50);

        builder.Property(x => x.Category)
            .HasConversion(new SnakeCaseEnumConverter<RejectionReasonCategory>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AdminLabel)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.UserMessageTemplate)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => x.AdminLabel)
            .IsUnique();

        builder.HasIndex(x => new { x.Category, x.IsActive })
            .HasFilter("is_active = true");
    }
}
