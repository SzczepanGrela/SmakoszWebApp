using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class ForbiddenWordConfiguration : IEntityTypeConfiguration<ForbiddenWord>
{
    public void Configure(EntityTypeBuilder<ForbiddenWord> builder)
    {
        builder.ToTable("forbidden_words", "system");

        builder.HasKey(x => x.WordId);

        builder.Property(x => x.Word)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasConversion(new SnakeCaseEnumConverter<ForbiddenWordCategory>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.AddedByUser)
            .WithMany()
            .HasForeignKey(x => x.AddedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Word)
            .IsUnique();
    }
}
