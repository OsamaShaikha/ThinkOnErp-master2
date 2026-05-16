using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChangesInterceptor for automatic data change audit logging.
/// Intercepts SaveChanges operations and logs entity changes (Added, Modified, Deleted) to the audit trail.
/// Integrates with IAuditLogger service and IAuditContextProvider for user context.
/// </summary>
public class EfCoreAuditInterceptor : SaveChangesInterceptor
{
    private readonly IAuditLogger _auditLogger;
    private readonly IAuditContextProvider _contextProvider;
    private readonly ILogger<EfCoreAuditInterceptor> _logger;

    public EfCoreAuditInterceptor(
        IAuditLogger auditLogger,
        IAuditContextProvider contextProvider,
        ILogger<EfCoreAuditInterceptor> logger)
    {
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Intercepts SaveChangesAsync and logs entity changes before they are persisted.
    /// </summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        try
        {
            await CaptureEntityChangesAsync(eventData.Context, cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't let audit logging failure break the database operation
            // Log the error but continue with SaveChanges
            _logger.LogError(ex, "Failed to capture entity changes for audit logging");
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Captures entity changes from the ChangeTracker and logs them to the audit trail.
    /// </summary>
    private async Task CaptureEntityChangesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added ||
                       e.State == EntityState.Modified ||
                       e.State == EntityState.Deleted)
            .ToList();

        if (!entries.Any())
        {
            return;
        }

        var auditEvents = new List<DataChangeAuditEvent>();

        foreach (var entry in entries)
        {
            var auditEvent = CreateAuditEvent(entry);
            if (auditEvent != null)
            {
                auditEvents.Add(auditEvent);
            }
        }

        // Log all audit events in a batch for better performance
        if (auditEvents.Any())
        {
            await _auditLogger.LogBatchAsync(auditEvents, cancellationToken);
        }
    }

    /// <summary>
    /// Creates an audit event from an entity entry.
    /// Returns null if the entity should not be audited (e.g., audit log tables).
    /// </summary>
    private DataChangeAuditEvent? CreateAuditEvent(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var entityType = entry.Entity.GetType();
        var tableName = entry.Metadata.GetTableName();

        // Skip audit log tables to prevent infinite recursion
        if (tableName?.Equals("SYS_AUDIT_LOG", StringComparison.OrdinalIgnoreCase) == true ||
            tableName?.Equals("SYS_AUDIT_LOG_ARCHIVE", StringComparison.OrdinalIgnoreCase) == true ||
            tableName?.Equals("SYS_AUDIT_STATUS_TRACKING", StringComparison.OrdinalIgnoreCase) == true)
        {
            return null;
        }

        var action = entry.State switch
        {
            EntityState.Added => "INSERT",
            EntityState.Modified => "UPDATE",
            EntityState.Deleted => "DELETE",
            _ => "UNKNOWN"
        };

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = _contextProvider.GetCorrelationId(),
            ActorType = _contextProvider.GetActorType(),
            ActorId = _contextProvider.GetActorId(),
            CompanyId = _contextProvider.GetCompanyId(),
            BranchId = _contextProvider.GetBranchId(),
            Action = action,
            EntityType = tableName ?? entityType.Name,
            EntityId = GetEntityId(entry),
            OldValue = entry.State == EntityState.Modified || entry.State == EntityState.Deleted
                ? SerializeEntityValues(entry.OriginalValues.Properties, entry.OriginalValues)
                : null,
            NewValue = entry.State == EntityState.Added || entry.State == EntityState.Modified
                ? SerializeEntityValues(entry.CurrentValues.Properties, entry.CurrentValues)
                : null,
            ChangedFields = entry.State == EntityState.Modified
                ? GetChangedFields(entry)
                : null,
            IpAddress = _contextProvider.GetIpAddress(),
            UserAgent = _contextProvider.GetUserAgent(),
            Timestamp = DateTime.UtcNow
        };

        return auditEvent;
    }

    /// <summary>
    /// Extracts the entity ID from the entry.
    /// Looks for common primary key property names (RowId, Id).
    /// </summary>
    private long? GetEntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        try
        {
            // Try to find primary key property
            var primaryKey = entry.Metadata.FindPrimaryKey();
            if (primaryKey != null && primaryKey.Properties.Count == 1)
            {
                var keyProperty = primaryKey.Properties[0];
                var keyValue = entry.Property(keyProperty.Name).CurrentValue;

                if (keyValue != null)
                {
                    // Try to convert to long
                    if (keyValue is long longValue)
                    {
                        return longValue;
                    }
                    else if (long.TryParse(keyValue.ToString(), out var parsedValue))
                    {
                        return parsedValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract entity ID from entry");
        }

        return null;
    }

    /// <summary>
    /// Serializes entity property values to JSON.
    /// </summary>
    private string? SerializeEntityValues(
        IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties,
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values)
    {
        try
        {
            var dictionary = new Dictionary<string, object?>();

            foreach (var property in properties)
            {
                var value = values[property];
                
                // Skip navigation properties and complex types
                if (property.IsShadowProperty() || property.IsForeignKey())
                {
                    continue;
                }

                // Convert byte arrays to base64 for JSON serialization
                if (value is byte[] byteArray)
                {
                    dictionary[property.Name] = Convert.ToBase64String(byteArray);
                }
                else
                {
                    dictionary[property.Name] = value;
                }
            }

            return JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize entity values");
            return null;
        }
    }

    /// <summary>
    /// Gets the dictionary of changed fields for modified entities.
    /// </summary>
    private Dictionary<string, object>? GetChangedFields(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        try
        {
            var changedFields = new Dictionary<string, object>();

            foreach (var property in entry.Properties)
            {
                if (property.IsModified)
                {
                    var currentValue = property.CurrentValue;
                    
                    // Convert byte arrays to base64
                    if (currentValue is byte[] byteArray)
                    {
                        changedFields[property.Metadata.Name] = Convert.ToBase64String(byteArray);
                    }
                    else if (currentValue != null)
                    {
                        changedFields[property.Metadata.Name] = currentValue;
                    }
                }
            }

            return changedFields.Any() ? changedFields : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get changed fields");
            return null;
        }
    }
}
