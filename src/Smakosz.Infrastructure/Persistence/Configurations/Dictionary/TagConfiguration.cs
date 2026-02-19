using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.Dictionary;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(x => x.TagId);

        builder.Property(x => x.TagName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TargetEntity)
            .HasConversion(new SnakeCaseEnumConverter<Domain.Enums.TagTargetEntity>())
            .HasMaxLength(20);

        builder.Property(x => x.DisplayColor)
            .HasMaxLength(20);

        builder.HasIndex(x => x.TagName)
            .IsUnique();

        builder.HasIndex(x => x.Category);
    }
}
