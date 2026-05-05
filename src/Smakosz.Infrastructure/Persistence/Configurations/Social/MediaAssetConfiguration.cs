using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Social;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.HasKey(x => x.AssetId);

        builder.Property(x => x.AssetId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PublicId)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasConversion(new SnakeCaseEnumConverter<MediaEntityType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Blurhash)
            .HasMaxLength(50);

        builder.Property(x => x.ModerationStatus)
            .HasColumnName("status")
            .HasConversion(new SnakeCaseEnumConverter<ContentModerationStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(200);

        builder.Property(x => x.CreditText)
            .HasMaxLength(100);

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.Uploader)
            .WithMany()
            .HasForeignKey(x => x.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.PublicId)
            .IsUnique();

        builder.HasIndex(x => x.UploadedBy);

        builder.HasIndex(x => x.EntityId)
            .HasFilter("entity_type = 'restaurant'")
            .HasDatabaseName("ix_media_assets_restaurant");

        builder.HasIndex(x => x.EntityId)
            .HasFilter("entity_type = 'dish'")
            .HasDatabaseName("ix_media_assets_dish");

        builder.HasIndex(x => x.EntityId)
            .HasFilter("entity_type = 'review'")
            .HasDatabaseName("ix_media_assets_review");

        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .HasFilter("is_primary = true")
            .HasDatabaseName("ix_media_assets_primary");

        builder.HasIndex(x => new { x.ModerationStatus, x.CreatedAt })
            .HasFilter("status IN ('pending', 'needs_review')")
            .HasDatabaseName("ix_media_assets_moderation");

        builder.HasIndex(x => x.AssetId)
            .HasFilter("entity_type = 'hero' AND status = 'approved'")
            .HasDatabaseName("ix_media_assets_hero");
    }
}
