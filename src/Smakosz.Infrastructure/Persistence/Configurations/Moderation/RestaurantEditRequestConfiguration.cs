using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Moderation;

public class RestaurantEditRequestConfiguration : IEntityTypeConfiguration<RestaurantEditRequest>
{
    public void Configure(EntityTypeBuilder<RestaurantEditRequest> builder)
    {
        builder.HasKey(x => x.RequestId);

        builder.HasQueryFilter(x => !x.User.IsDeleted);

        builder.Property(x => x.Status)
            .HasConversion(new SnakeCaseEnumConverter<EditRequestStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ChangeType)
            .HasConversion(new SnakeCaseEnumConverter<EditRequestChangeType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ChangeScope)
            .HasConversion(new SnakeCaseEnumConverter<EditRequestChangeScope>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.NewName)
            .HasMaxLength(255);

        builder.Property(x => x.NewDescription)
            .HasMaxLength(1000);

        builder.Property(x => x.NewAddress)
            .HasMaxLength(200);

        builder.Property(x => x.NewCuisineType)
            .HasMaxLength(100);

        builder.Property(x => x.NewPhone)
            .HasMaxLength(20);

        builder.Property(x => x.NewWebsite)
            .HasMaxLength(200);

        builder.Property(x => x.NewImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.NewImageBlurhash)
            .HasMaxLength(50);

        builder.Property(x => x.ModerationStatus)
            .HasConversion(new SnakeCaseEnumConverter<ContentModerationStatus>())
            .HasMaxLength(20);

        builder.Property(x => x.AdminNote)
            .HasMaxLength(500);

        builder.Property(x => x.Version)
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.ResolvedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ResolvedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Status)
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ix_restaurant_edit_requests_status");

        builder.HasIndex(x => new { x.RestaurantId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.ChangeType, x.Status });
    }
}
