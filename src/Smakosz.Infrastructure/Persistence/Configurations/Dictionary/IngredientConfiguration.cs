using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasKey(x => x.IngredientId);

        builder.Property(x => x.IngredientName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IconUrl)
            .HasMaxLength(500);

        builder.Property(x => x.IconBlurhash)
            .HasMaxLength(50);

        builder.HasIndex(x => x.IngredientName)
            .IsUnique();
    }
}
