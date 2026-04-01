using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", "system");

        builder.HasKey(x => x.TokenId);

        builder.Property(x => x.TokenId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.DeviceInfo)
            .HasMaxLength(255);

        builder.Property(x => x.IpAddress)
            .HasConversion(new InetStringConverter())
            .HasColumnType("inet");

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.RevokedAt });

        builder.HasIndex(x => x.ExpiresAt)
            .HasFilter("revoked_at IS NULL");
    }
}
