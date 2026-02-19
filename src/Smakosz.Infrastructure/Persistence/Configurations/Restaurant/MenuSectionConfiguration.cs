using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class MenuSectionConfiguration : IEntityTypeConfiguration<MenuSection>
{
    public void Configure(EntityTypeBuilder<MenuSection> builder)
    {
        builder.HasKey(x => x.SectionId);

        builder.Property(x => x.SectionName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0);

        builder.HasOne(x => x.Restaurant)
            .WithMany(r => r.MenuSections)
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.RestaurantId, x.SectionName })
            .IsUnique();

        builder.HasIndex(x => new { x.RestaurantId, x.DisplayOrder });
    }
}
