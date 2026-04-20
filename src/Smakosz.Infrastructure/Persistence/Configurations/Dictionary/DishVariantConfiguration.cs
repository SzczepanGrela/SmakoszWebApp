using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.Generator;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class DishVariantConfiguration : IEntityTypeConfiguration<DishVariant>
{
    public void Configure(EntityTypeBuilder<DishVariant> builder)
    {
        builder.HasKey(x => x.VariantId);

        builder.Property(x => x.VariantName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne(x => x.Archetype)
            .WithMany()
            .HasForeignKey(x => x.ArchetypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.VariantName, x.ArchetypeId })
            .IsUnique();
    }
}
