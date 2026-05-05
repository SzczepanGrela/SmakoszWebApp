using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Identity;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.HasKey(x => x.PushSubscriptionId);

        builder.HasQueryFilter(x => !x.User.IsDeleted);

        builder.Property(x => x.Endpoint)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.P256dh)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.Auth)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.DeviceName)
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Endpoint)
            .IsUnique();

        builder.HasIndex(x => x.UserId);
    }
}
