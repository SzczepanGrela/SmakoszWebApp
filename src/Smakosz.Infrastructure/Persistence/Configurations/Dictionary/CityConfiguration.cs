using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.HasKey(x => x.CityId);

        builder.Property(x => x.CityName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Region)
            .HasMaxLength(100);

        builder.HasIndex(x => x.CityName)
            .IsUnique();
    }
}
