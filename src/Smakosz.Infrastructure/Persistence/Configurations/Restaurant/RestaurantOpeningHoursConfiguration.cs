using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Restaurant;

public class RestaurantOpeningHoursConfiguration : IEntityTypeConfiguration<RestaurantOpeningHours>
{
    public void Configure(EntityTypeBuilder<RestaurantOpeningHours> builder)
    {
        builder.HasKey(x => x.HoursId);

        builder.Property(x => x.DayOfWeek)
            .IsRequired();

        builder.Property(x => x.OpenTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(x => x.CloseTime)
            .HasColumnType("time")
            .IsRequired();

        builder.HasOne(x => x.Restaurant)
            .WithMany(r => r.OpeningHours)
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RestaurantId);

        builder.HasIndex(x => new { x.RestaurantId, x.DayOfWeek });
    }
}
