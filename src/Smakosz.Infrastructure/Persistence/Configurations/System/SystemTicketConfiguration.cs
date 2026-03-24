using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class SystemTicketConfiguration : IEntityTypeConfiguration<SystemTicket>
{
    public void Configure(EntityTypeBuilder<SystemTicket> builder)
    {
        builder.ToTable("tickets", "system");

        builder.HasKey(x => x.TicketId);

        builder.Property(x => x.TicketType)
            .HasConversion(new SnakeCaseEnumConverter<TicketType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ReferenceId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(new SnakeCaseEnumConverter<TicketStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.Priority)
            .HasDefaultValue(3);

        builder.Property(x => x.Description)
            .HasMaxLength(5000);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        // Cross-schema FK to public.users
        builder.HasOne(x => x.AssignedAdmin)
            .WithMany()
            .HasForeignKey(x => x.AssignedAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => new { x.TicketType, x.ReferenceId });

        builder.HasIndex(x => new { x.Status, x.Priority })
            .IsDescending(false, true);

        builder.HasIndex(x => x.AssignedAdminId)
            .HasFilter("assigned_admin_id IS NOT NULL");
    }
}
