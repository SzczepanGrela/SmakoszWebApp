using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(x => x.NotificationId);

        builder.Property(x => x.PublicId)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion(new SnakeCaseEnumConverter<NotificationType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb");

        builder.Property(x => x.Priority)
            .HasDefaultValue(1);

        builder.Property(x => x.GroupKey)
            .HasMaxLength(200);

        builder.Property(x => x.Counter)
            .HasDefaultValue(1);

        builder.Property(x => x.EmailStatus)
            .HasConversion(new SnakeCaseEnumConverter<EmailStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.PushStatus)
            .HasConversion(new SnakeCaseEnumConverter<PushStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.Severity)
            .HasConversion(new SnakeCaseEnumConverter<NotificationSeverity>())
            .HasMaxLength(20);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.PublicId)
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.UserId, x.GroupKey })
            .IsUnique()
            .HasFilter("is_read = false AND is_deleted = false AND group_key IS NOT NULL")
            .HasDatabaseName("ix_notifications_group_key_unique");

        builder.HasIndex(x => x.UserId)
            .HasFilter("is_read = false AND is_deleted = false")
            .HasDatabaseName("ix_notifications_badge");
    }
}
