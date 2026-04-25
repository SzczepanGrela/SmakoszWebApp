using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.Generator;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class DishArchetypeConfiguration : IEntityTypeConfiguration<DishArchetype>
{
    public void Configure(EntityTypeBuilder<DishArchetype> builder)
    {
        builder.HasKey(x => x.ArchetypeId);

        builder.Property(x => x.ArchetypeName)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.ArchetypeName)
            .IsUnique();
    }
}
