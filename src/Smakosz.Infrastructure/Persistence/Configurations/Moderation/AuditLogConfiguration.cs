using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Moderation;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.AuditLogId);

        builder.Property(x => x.AuditLogId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TableName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RecordId)
            .IsRequired();

        builder.Property(x => x.Operation)
            .HasConversion(new UpperCaseEnumConverter<AuditOperation>())
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.ChangedBy)
            .HasMaxLength(100)
            .HasDefaultValue("system");

        builder.Property(x => x.ChangedAt)
            .HasDefaultValueSql("now()");

        builder.Property(x => x.OldValues)
            .HasColumnType("jsonb");

        builder.Property(x => x.NewValues)
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(x => new { x.TableName, x.RecordId });

        builder.HasIndex(x => x.ChangedAt)
            .IsDescending(true);

        builder.HasIndex(x => new { x.TableName, x.ChangedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => x.ChangedBy);
    }
}
