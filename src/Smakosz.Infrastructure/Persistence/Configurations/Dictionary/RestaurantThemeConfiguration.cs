using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class RestaurantThemeConfiguration : IEntityTypeConfiguration<RestaurantTheme>
{
    public void Configure(EntityTypeBuilder<RestaurantTheme> builder)
    {
        builder.HasKey(x => x.ThemeId);

        builder.Property(x => x.PublicId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Icon)
            .HasMaxLength(10);

        builder.Property(x => x.Weight)
            .IsRequired();

        builder.Property(x => x.Prompt)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasIndex(x => x.CuisineTypeId);

        builder.HasOne(x => x.Cuisine)
            .WithMany()
            .HasForeignKey(x => x.CuisineTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
