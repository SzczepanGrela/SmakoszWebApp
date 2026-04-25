namespace Smakosz.Domain.Interfaces;

/// <summary>
/// Marker interface for entities that are written exclusively by the tools/generator/
/// Python pipeline to produce synthetic data for NCF training and E2E tests. Entities
/// implementing this interface must not be queried by runtime business logic.
/// </summary>
public interface IGeneratorOnlyEntity;
