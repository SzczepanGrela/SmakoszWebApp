using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class SecurityLogConfiguration : IEntityTypeConfiguration<SecurityLog>
{
    public void Configure(EntityTypeBuilder<SecurityLog> builder)
    {
        builder.ToTable("security_logs", "system");

        builder.HasKey(x => x.LogId);

        builder.Property(x => x.LogId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EventType)
            .HasConversion(new SnakeCaseEnumConverter<SecurityEventType>())
            .HasMaxLength(50);

        builder.Property(x => x.IpAddress)
            .HasConversion(new InetStringConverter())
            .HasColumnType("inet");

        builder.Property(x => x.Email)
            .HasMaxLength(100);

        builder.Property(x => x.Details)
            .HasColumnType("jsonb");

        builder.Property(x => x.CountryCode)
            .HasMaxLength(2);

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.EventType, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => x.IpAddress);
    }
}
