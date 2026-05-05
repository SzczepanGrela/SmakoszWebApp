using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Identity;

public class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.HasKey(x => new { x.FollowerId, x.FollowedId });

        builder.HasQueryFilter(x => !x.Follower.IsDeleted && !x.Followed.IsDeleted);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Follower)
            .WithMany()
            .HasForeignKey(x => x.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Followed)
            .WithMany()
            .HasForeignKey(x => x.FollowedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.FollowedId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.FollowerId, x.CreatedAt })
            .IsDescending(false, true);
    }
}
