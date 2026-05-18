namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Audit event for data modification operations (INSERT, UPDATE, DELETE).
/// Captures before and after values for compliance and debugging.
/// </summary>
public class DataChangeAuditEvent : AuditEvent
{
    /// <summary>
    /// JSON representation of the entity state before the change (null for INSERT)
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// JSON representation of the entity state after the change (null for DELETE)
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Dictionary of changed fields with their new values (for UPDATE operations)
    /// </summary>
    public Dictionary<string, object>? ChangedFields { get; set; }

    /// <summary>
    /// Event category (DataChange, Authentication, Permission, Exception, Configuration, Request)
    /// Default: "DataChange". Override to categorize the audit event differently.
    /// </summary>
    public string EventCategory { get; set; } = "DataChange";

    /// <summary>
    /// Additional metadata in JSON format for extensibility.
    /// Stores structured context about the data change (e.g., ticket details, user info).
    /// </summary>
    public string? Metadata { get; set; }
}
