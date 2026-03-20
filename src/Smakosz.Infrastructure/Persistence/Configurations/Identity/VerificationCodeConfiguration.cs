using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Identity;

public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.HasKey(x => x.VerificationCodeId);

        builder.Property(x => x.CodeHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion(new SnakeCaseEnumConverter<VerificationCodeType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasMaxLength(255);

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.AttemptsCount)
            .HasDefaultValue(0);

        builder.HasOne(x => x.User)
            .WithMany(u => u.VerificationCodes)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CodeHash);
        builder.HasIndex(x => x.UserId);
    }
}
