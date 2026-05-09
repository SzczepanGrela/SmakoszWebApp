using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class UserRecommendationCacheConfiguration : IEntityTypeConfiguration<UserRecommendationCache>
{
    public void Configure(EntityTypeBuilder<UserRecommendationCache> builder)
    {
        builder.ToTable("user_recommendation_cache", "system");

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(x => x.TopDishIdsJson).HasColumnName("top_dish_ids").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ModelVersion).HasColumnName("model_version").IsRequired();
        builder.Property(x => x.GeneratedAt).HasColumnName("generated_at").HasDefaultValueSql("now()");

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserRecommendationCache>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ModelVersion).HasDatabaseName("ix_user_rec_cache_model_version");
    }
}
