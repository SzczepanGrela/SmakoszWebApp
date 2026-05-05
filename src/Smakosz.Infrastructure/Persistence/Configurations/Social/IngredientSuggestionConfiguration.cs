using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class IngredientSuggestionConfiguration : IEntityTypeConfiguration<IngredientSuggestion>
{
    public void Configure(EntityTypeBuilder<IngredientSuggestion> builder)
    {
        builder.HasKey(x => x.SuggestionId);

        builder.HasQueryFilter(x => x.UserId == null || !x.User!.IsDeleted);

        builder.Property(x => x.SuggestedName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IconUrl)
            .HasMaxLength(500);

        builder.Property(x => x.IconBlurhash)
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion(new SnakeCaseEnumConverter<IngredientSuggestionStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.MergedIngredient)
            .WithMany()
            .HasForeignKey(x => x.MergedIngredientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ix_ingredient_suggestions_status");
    }
}
