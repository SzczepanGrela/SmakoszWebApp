using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class BannedIdentifierConfiguration : IEntityTypeConfiguration<BannedIdentifier>
{
    public void Configure(EntityTypeBuilder<BannedIdentifier> builder)
    {
        builder.ToTable("banned_identifiers", "system");

        builder.HasKey(x => x.BanId);

        builder.Property(x => x.Type)
            .HasConversion(new SnakeCaseEnumConverter<BannedIdentifierType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Value)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.BannedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.BannedByUser)
            .WithMany()
            .HasForeignKey(x => x.BannedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.Type, x.Value })
            .IsUnique();
    }
}
