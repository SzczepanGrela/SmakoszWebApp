using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class RestaurantTagConfiguration : IEntityTypeConfiguration<RestaurantTag>
{
    public void Configure(EntityTypeBuilder<RestaurantTag> builder)
    {
        builder.HasKey(x => new { x.RestaurantId, x.TagId });

        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tag)
            .WithMany()
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
