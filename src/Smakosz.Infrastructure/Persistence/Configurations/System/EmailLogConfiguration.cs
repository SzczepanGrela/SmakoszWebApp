using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_logs", "system");

        builder.HasKey(x => x.LogId);

        builder.Property(x => x.LogId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Type)
            .HasMaxLength(50);

        builder.Property(x => x.Recipient)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .HasDefaultValue("pending");

        builder.Property(x => x.Provider)
            .HasMaxLength(50);

        builder.Property(x => x.ProviderMessageId)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => new { x.Recipient, x.CreatedAt })
            .IsDescending(false, true);
    }
}
