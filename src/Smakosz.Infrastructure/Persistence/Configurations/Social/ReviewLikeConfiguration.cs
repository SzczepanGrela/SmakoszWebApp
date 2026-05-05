using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class ReviewLikeConfiguration : IEntityTypeConfiguration<ReviewLike>
{
    public void Configure(EntityTypeBuilder<ReviewLike> builder)
    {
        builder.HasKey(x => new { x.UserId, x.ReviewId });

        builder.HasQueryFilter(x => !x.Review.IsDeleted);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Review)
            .WithMany()
            .HasForeignKey(x => x.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => x.ReviewId);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true);
    }
}
