using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class RestaurantConfiguration : IEntityTypeConfiguration<Domain.Entities.Restaurant>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Restaurant> builder)
    {
        builder.HasKey(x => x.RestaurantId);

        builder.Property(x => x.PublicId)
            .IsRequired();

        builder.Property(x => x.RestaurantName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CuisineType)
            .HasMaxLength(100);

        builder.Property(x => x.Address)
            .HasMaxLength(200);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(10);

        builder.Property(x => x.Latitude)
            .HasColumnType("numeric(10,7)");

        builder.Property(x => x.Longitude)
            .HasColumnType("numeric(10,7)");

        builder.Property(x => x.GeocodeSource)
            .HasConversion(new SnakeCaseEnumConverter<GeocodeSource>())
            .HasMaxLength(20);

        builder.Property(x => x.Phone)
            .HasMaxLength(20);

        builder.Property(x => x.Email)
            .HasMaxLength(255);

        builder.Property(x => x.Website)
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Slug)
            .HasMaxLength(255);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.ImageBlurhash)
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion(new SnakeCaseEnumConverter<RestaurantStatus>())
            .HasMaxLength(50);

        builder.Property(x => x.TrendingScore)
            .HasColumnType("numeric(10,4)");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        builder.Property(x => x.SecretAmbianceType)
            .HasMaxLength(100);

        builder.Property(x => x.SecretArchetypeModifiers)
            .HasColumnType("jsonb");

        builder.Property(x => x.SecretMenuBlueprint)
            .HasMaxLength(100);

        // FK
        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId);

        builder.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => x.PublicId)
            .IsUnique();

        builder.HasIndex(x => x.RestaurantName)
            .IsUnique();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.HasIndex(x => x.CityId);
        builder.HasIndex(x => x.CuisineType);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => new { x.IsVerified, x.OwnerId });
    }
}
