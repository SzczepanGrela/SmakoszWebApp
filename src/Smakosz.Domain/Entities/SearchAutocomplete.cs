namespace Smakosz.Domain.Entities;

// Backed by the SQL view search_autocomplete which unions restaurants and dishes
// with a precomputed name_normalized column produced by f_unaccent so trigram
// indexes on the base tables get used when the suggest handler filters by similarity.
public class SearchAutocomplete
{
    public string Type { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }
    public string? ImageBlurhash { get; set; }
    public string NameNormalized { get; set; } = string.Empty;
    public int Priority { get; set; }
}
