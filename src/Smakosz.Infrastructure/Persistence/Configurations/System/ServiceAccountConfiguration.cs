using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class ServiceAccountConfiguration : IEntityTypeConfiguration<ServiceAccount>
{
    public void Configure(EntityTypeBuilder<ServiceAccount> builder)
    {
        builder.ToTable("service_accounts", "system");

        builder.HasKey(x => x.AccountId);

        builder.Property(x => x.ServiceName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Permissions)
            .HasColumnType("jsonb");

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => x.ServiceName)
            .IsUnique();
    }
}
