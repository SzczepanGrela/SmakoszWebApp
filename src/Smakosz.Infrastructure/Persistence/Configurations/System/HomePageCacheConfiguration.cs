using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class HomePageCacheConfiguration : IEntityTypeConfiguration<HomePageCache>
{
    public void Configure(EntityTypeBuilder<HomePageCache> builder)
    {
        builder.ToTable("home_page_cache", "system");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TrendingRestaurantsJson).HasColumnType("text");
        builder.Property(x => x.TrendingDishesJson).HasColumnType("text");
        builder.Property(x => x.TopRatedDishesJson).HasColumnType("text");
        builder.Property(x => x.RecentReviewsJson).HasColumnType("text");
        builder.Property(x => x.PopularCategoriesJson).HasColumnType("text");
        builder.Property(x => x.HeroImageJson).HasColumnType("text");
        builder.Property(x => x.NewestRestaurantsJson).HasColumnType("text");
        builder.Property(x => x.MostReviewedDishesJson).HasColumnType("text");

        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("now()");

        builder.HasData(new HomePageCache { Id = 1 });
    }
}
