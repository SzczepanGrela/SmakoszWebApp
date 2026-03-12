using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class SystemLogConfiguration : IEntityTypeConfiguration<SystemLog>
{
    public void Configure(EntityTypeBuilder<SystemLog> builder)
    {
        builder.ToTable("logs", "system");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Source)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Level)
            .HasConversion(new UpperCaseEnumConverter<Domain.Enums.LogLevel>())
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Message)
            .IsRequired();

        builder.Property(x => x.Context)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.CreatedAt)
            .IsDescending(true);

        builder.HasIndex(x => new { x.Level, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.Source, x.CreatedAt })
            .IsDescending(false, true);
    }
}
