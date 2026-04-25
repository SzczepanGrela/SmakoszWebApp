using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities.Generator;

/// <summary>
/// Generator-only entity. Populated by tools/generator/ Python pipeline for synthetic
/// data. See ARCHETYPE_TO_CATEGORY in phase3_dishes.py for the mapping from archetype
/// to dish_category tag.
/// </summary>
public class DishArchetype : IGeneratorOnlyEntity
{
    public int ArchetypeId { get; set; }
    public string ArchetypeName { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
