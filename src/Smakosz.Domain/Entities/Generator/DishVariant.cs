using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities.Generator;

/// <summary>
/// Generator-only entity. Populated by tools/generator/ Python pipeline. Referenced by
/// Dish.SecretVariantId which is NULL in production data.
/// </summary>
public class DishVariant : IGeneratorOnlyEntity
{
    public int VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public int ArchetypeId { get; set; }

    public DishArchetype Archetype { get; set; } = null!;
}
