using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.UserId);

        builder.Property(x => x.PublicId)
            .IsRequired();

        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.SecurityStamp)
            .HasMaxLength(50);

        builder.Property(x => x.FirstName)
            .HasMaxLength(50);

        builder.Property(x => x.LastName)
            .HasMaxLength(50);

        builder.Property(x => x.FullName)
            .HasMaxLength(100);

        builder.Property(x => x.Phone)
            .HasMaxLength(20);

        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(x => x.AvatarBlurhash)
            .HasMaxLength(50);

        builder.Property(x => x.DateOfBirth)
            .HasColumnType("date");

        builder.Property(x => x.Role)
            .HasConversion(new SnakeCaseEnumConverter<UserRole>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(100);

        builder.Property(x => x.SecretRatingBaseline)
            .HasDefaultValue(6.0);

        builder.Property(x => x.SecretEnjoyedArchetypes)
            .HasColumnType("jsonb");

        builder.Property(x => x.SecretCharacteristicsVector)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.SecretIngredientPreferences)
            .HasColumnType("jsonb");

        builder.Property(x => x.SecretCleanlinessPreference)
            .HasColumnType("jsonb");

        builder.Property(x => x.SecretPreferredAmbiance)
            .HasMaxLength(100);

        // FK - circular FK to Restaurant, use ClientSetNull to avoid cascade cycle
        builder.HasOne<Domain.Entities.Restaurant>()
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(x => x.HomeCity)
            .WithMany()
            .HasForeignKey(x => x.HomeCityId);

        builder.HasOne(x => x.NotificationSettings)
            .WithOne(ns => ns.User)
            .HasForeignKey<UserNotificationSettings>(ns => ns.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.PublicId)
            .IsUnique();

        builder.HasIndex(x => x.Username)
            .IsUnique();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.HasIndex(x => x.RestaurantId)
            .IsUnique();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.HasIndex(x => x.HomeCityId);
        builder.HasIndex(x => x.Role);

        builder.HasIndex(x => x.Email)
            .HasFilter("is_active = true AND is_deleted = false")
            .HasDatabaseName("ix_users_active_login");

        builder.HasIndex(x => x.SecretIsInfluencer)
            .HasFilter("secret_is_influencer = true");
    }
}
