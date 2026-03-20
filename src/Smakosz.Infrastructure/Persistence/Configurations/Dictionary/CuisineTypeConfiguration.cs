using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class CuisineTypeConfiguration : IEntityTypeConfiguration<CuisineType>
{
    public void Configure(EntityTypeBuilder<CuisineType> builder)
    {
        builder.HasKey(x => x.CuisineTypeId);

        builder.Property(x => x.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Icon)
            .HasMaxLength(10);

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}
