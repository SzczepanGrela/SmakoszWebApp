using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class FileToDeleteConfiguration : IEntityTypeConfiguration<FileToDelete>
{
    public void Configure(EntityTypeBuilder<FileToDelete> builder)
    {
        builder.ToTable("files_to_delete", "system");

        builder.HasKey(x => x.FileId);

        builder.Property(x => x.FileId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.R2Key)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Bucket)
            .HasMaxLength(100)
            .HasDefaultValue("smakosz-photos");

        builder.Property(x => x.Reason)
            .HasMaxLength(50);

        builder.Property(x => x.SourceEntity)
            .HasMaxLength(50);

        builder.Property(x => x.QueuedAt)
            .HasDefaultValueSql("now()");
    }
}
