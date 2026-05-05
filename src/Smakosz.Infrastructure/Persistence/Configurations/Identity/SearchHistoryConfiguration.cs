using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations.Identity;

public class SearchHistoryConfiguration : IEntityTypeConfiguration<SearchHistory>
{
    public void Configure(EntityTypeBuilder<SearchHistory> builder)
    {
        builder.HasKey(x => x.SearchId);

        builder.HasQueryFilter(x => x.UserId == null || !x.User!.IsDeleted);

        builder.Property(x => x.SearchQuery)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.SearchHistories)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true);
    }
}
