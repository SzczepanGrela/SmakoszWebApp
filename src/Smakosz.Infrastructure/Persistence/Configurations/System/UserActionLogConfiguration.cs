using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class UserActionLogConfiguration : IEntityTypeConfiguration<UserActionLog>
{
    public void Configure(EntityTypeBuilder<UserActionLog> builder)
    {
        builder.ToTable("user_action_logs", "system");

        builder.HasKey(x => x.ActionLogId);

        builder.Property(x => x.ActionLogId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ActionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.OldValue).HasMaxLength(255);
        builder.Property(x => x.NewValue).HasMaxLength(255);
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true);
    }
}
