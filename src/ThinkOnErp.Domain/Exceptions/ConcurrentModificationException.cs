namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when a concurrent modification conflict is detected.
/// </summary>
public class ConcurrentModificationException : DomainException
{
    public string EntityType { get; }
    public Int64 EntityId { get; }

    public ConcurrentModificationException(string entityType, Int64 entityId) 
        : base($"The {entityType} with ID {entityId} was modified by another user. Please refresh and try again.", "CONCURRENT_MODIFICATION")
    {
        EntityType = entityType;
        EntityId = entityId;
        AddContext("EntityType", entityType);
        AddContext("EntityId", entityId);
    }
}
