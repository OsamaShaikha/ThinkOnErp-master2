namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Provides the serialized state of an entity before a data change operation.
/// Used by audit logging to capture old values for UPDATE and DELETE operations.
/// </summary>
public interface IEntityStateProvider
{
    /// <summary>
    /// Gets the serialized JSON state of an entity by its type name and ID.
    /// Returns null if the entity is not found or cannot be serialized.
    /// </summary>
    Task<string?> GetEntityStateAsync(string entityType, long entityId);
}
