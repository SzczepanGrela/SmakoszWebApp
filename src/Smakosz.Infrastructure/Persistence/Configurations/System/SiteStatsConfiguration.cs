using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class SiteStatsConfiguration : IEntityTypeConfiguration<SiteStats>
{
    public void Configure(EntityTypeBuilder<SiteStats> builder)
    {
        builder.ToTable("site_stats", "system");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MostPopularCuisine)
            .HasMaxLength(100);

        builder.Property(x => x.MostActiveCity)
            .HasMaxLength(100);

        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("now()");

        builder.HasData(new SiteStats { Id = 1 });
    }
}
