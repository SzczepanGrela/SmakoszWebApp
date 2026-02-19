using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class DishSectionAssignmentConfiguration : IEntityTypeConfiguration<DishSectionAssignment>
{
    public void Configure(EntityTypeBuilder<DishSectionAssignment> builder)
    {
        builder.HasKey(x => new { x.DishId, x.SectionId });

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Dish)
            .WithMany()
            .HasForeignKey(x => x.DishId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SectionId, x.DisplayOrder });
        builder.HasIndex(x => x.DishId);
    }
}
