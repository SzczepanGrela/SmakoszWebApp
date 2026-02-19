namespace Smakosz.Domain.Entities;

public class DishVariant
{
    public int VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public int ArchetypeId { get; set; }

    public DishArchetype Archetype { get; set; } = null!;
}
