namespace Smakosz.Domain.Entities;

public class DishArchetype
{
    public int ArchetypeId { get; set; }
    public string ArchetypeName { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
