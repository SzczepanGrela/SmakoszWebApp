using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class FavoriteRestaurantConfiguration : IEntityTypeConfiguration<FavoriteRestaurant>
{
    public void Configure(EntityTypeBuilder<FavoriteRestaurant> builder)
    {
        builder.HasKey(x => new { x.UserId, x.RestaurantId });

        builder.HasQueryFilter(x => !x.User.IsDeleted);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => x.RestaurantId);
    }
}
