using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(x => x.ReviewId);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.PublicId)
            .IsRequired();

        builder.Property(x => x.VisitDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.DishRating)
            .IsRequired();

        builder.Property(x => x.ServiceRating)
            .IsRequired();

        builder.Property(x => x.CleanlinessRating)
            .IsRequired();

        builder.Property(x => x.AmbianceRating)
            .IsRequired();

        builder.Property(x => x.ModerationStatus)
            .HasColumnName("content_status")
            .HasConversion(new SnakeCaseEnumConverter<ContentModerationStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.ContentRejectionReason)
            .HasMaxLength(200);

        builder.Property(x => x.HelpfulCount)
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        // FK
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Dish)
            .WithMany()
            .HasForeignKey(x => x.DishId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.PublicId)
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.DishId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.RestaurantId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.ModerationStatus, x.CreatedAt })
            .HasFilter("content_status IN ('pending', 'needs_review')")
            .HasDatabaseName("ix_reviews_content_status");
    }
}
