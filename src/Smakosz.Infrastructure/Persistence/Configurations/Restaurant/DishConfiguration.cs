using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class DishConfiguration : IEntityTypeConfiguration<Dish>
{
    public void Configure(EntityTypeBuilder<Dish> builder)
    {
        builder.HasKey(x => x.DishId);

        builder.Property(x => x.PublicId)
            .IsRequired();

        builder.Property(x => x.DishName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasColumnType("numeric(10,2)");

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Slug)
            .HasMaxLength(255);

        builder.Property(x => x.TrendingScore)
            .HasColumnType("numeric(10,4)");

        builder.Property(x => x.IngredientsJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.ImageBlurhash)
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ReviewCount)
            .HasDefaultValue(0);

        builder.Property(x => x.ModerationStatus)
            .HasConversion(new SnakeCaseEnumConverter<ContentModerationStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.SecretBasePrice)
            .HasColumnType("numeric(10,2)");

        builder.Property(x => x.SecretCharacteristicsVector)
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(x => x.SecretPenaltyVector)
            .HasColumnType("jsonb");

        // FK
        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId);

        builder.HasOne(x => x.SecretVariant)
            .WithMany()
            .HasForeignKey(x => x.SecretVariantId);

        // Indexes
        builder.HasIndex(x => x.PublicId)
            .IsUnique();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.HasIndex(x => x.RestaurantId);
        builder.HasIndex(x => x.Price);
        builder.HasIndex(x => x.IsAvailable);
        builder.HasIndex(x => x.SecretVariantId);
    }
}
