using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class SavedDishConfiguration : IEntityTypeConfiguration<SavedDish>
{
    public void Configure(EntityTypeBuilder<SavedDish> builder)
    {
        builder.HasKey(x => new { x.UserId, x.DishId });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Dish)
            .WithMany()
            .HasForeignKey(x => x.DishId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DishId);
    }
}
