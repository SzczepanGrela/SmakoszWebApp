using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Persistence.Configurations;

public class SearchAutocompleteConfiguration : IEntityTypeConfiguration<SearchAutocomplete>
{
    public void Configure(EntityTypeBuilder<SearchAutocomplete> builder)
    {
        builder.ToView("search_autocomplete").HasNoKey();
    }
}
