using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("config", "system");

        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key)
            .HasMaxLength(50);

        builder.Property(x => x.Value)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.Key, x.Value })
            .HasFilter("is_public = true")
            .HasDatabaseName("ix_config_public");
    }
}
