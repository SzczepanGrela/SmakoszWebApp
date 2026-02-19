using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class DishIngredientConfiguration : IEntityTypeConfiguration<DishIngredient>
{
    public void Configure(EntityTypeBuilder<DishIngredient> builder)
    {
        builder.HasKey(x => new { x.DishId, x.IngredientId });

        builder.HasOne(x => x.Dish)
            .WithMany()
            .HasForeignKey(x => x.DishId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Ingredient)
            .WithMany()
            .HasForeignKey(x => x.IngredientId);

        builder.HasIndex(x => x.IngredientId);
    }
}
