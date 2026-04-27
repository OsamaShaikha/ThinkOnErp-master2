using System.Text.Json;
using ThinkOnErp.Domain.Entities.Audit;

namespace ThinkOnErp.Examples;

/// <summary>
/// Example usage of DataChangeAuditEvent class for different database operations
/// </summary>
public class DataChangeAuditEventUsageExample
{
    /// <summary>
    /// Example of creating an audit event for an INSERT operation
    /// </summary>
    public static DataChangeAuditEvent CreateInsertAuditEvent()
    {
        var newUser = new 
        { 
            Id = 123, 
            Name = "John Doe", 
            Email = "john.doe@example.com",
            CompanyId = 1,
            BranchId = 2,
            IsActive = true
        };

        return new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "COMPANY_ADMIN",
            ActorId = 456,
            CompanyId = 1,
            BranchId = 2,
            Action = "INSERT",
            EntityType = "User",
            EntityId = 123,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            Timestamp = DateTime.UtcNow,
            
            // INSERT operation: no old value, only new value
            OldValue = null,
            NewValue = JsonSerializer.Serialize(newUser),
            ChangedFields = null // Not applicable for INSERT
        };
    }

    /// <summary>
    /// Example of creating an audit event for an UPDATE operation
    /// </summary>
    public static DataChangeAuditEvent CreateUpdateAuditEvent()
    {
        var oldUser = new 
        { 
            Id = 123, 
            Name = "John Doe", 
            Email = "john.doe@example.com",
            CompanyId = 1,
            BranchId = 2,
            IsActive = true
        };

        var newUser = new 
        { 
            Id = 123, 
            Name = "John Smith", // Changed
            Email = "john.smith@example.com", // Changed
            CompanyId = 1,
            BranchId = 2,
            IsActive = false // Changed
        };

        return new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 789,
            CompanyId = 1,
            BranchId = 2,
            Action = "UPDATE",
            EntityType = "User",
            EntityId = 123,
            IpAddress = "10.0.0.50",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            Timestamp = DateTime.UtcNow,
            
            // UPDATE operation: both old and new values
            OldValue = JsonSerializer.Serialize(oldUser),
            NewValue = JsonSerializer.Serialize(newUser),
            
            // Track which specific fields changed
            ChangedFields = new Dictionary<string, object>
            {
                { "Name", "John Smith" },
                { "Email", "john.smith@example.com" },
                { "IsActive", false }
            }
        };
    }

    /// <summary>
    /// Example of creating an audit event for a DELETE operation
    /// </summary>
    public static DataChangeAuditEvent CreateDeleteAuditEvent()
    {
        var deletedUser = new 
        { 
            Id = 123, 
            Name = "John Smith", 
            Email = "john.smith@example.com",
            CompanyId = 1,
            BranchId = 2,
            IsActive = false
        };

        return new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SUPER_ADMIN",
            ActorId = 1,
            CompanyId = 1,
            BranchId = null, // Super admin can delete across branches
            Action = "DELETE",
            EntityType = "User",
            EntityId = 123,
            IpAddress = "172.16.0.10",
            UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36",
            Timestamp = DateTime.UtcNow,
            
            // DELETE operation: only old value, no new value
            OldValue = JsonSerializer.Serialize(deletedUser),
            NewValue = null,
            ChangedFields = null // Not applicable for DELETE
        };
    }

    /// <summary>
    /// Example of creating an audit event for a complex object with nested properties
    /// </summary>
    public static DataChangeAuditEvent CreateComplexObjectAuditEvent()
    {
        var oldCompany = new 
        { 
            Id = 1,
            Name = "Acme Corp",
            Address = new 
            {
                Street = "123 Main St",
                City = "New York",
                Country = "USA"
            },
            Settings = new 
            {
                Currency = "USD",
                TimeZone = "EST",
                Features = new[] { "POS", "HR" }
            }
        };

        var newCompany = new 
        { 
            Id = 1,
            Name = "Acme Corporation", // Changed
            Address = new 
            {
                Street = "456 Broadway", // Changed
                City = "New York",
                Country = "USA"
            },
            Settings = new 
            {
                Currency = "USD",
                TimeZone = "PST", // Changed
                Features = new[] { "POS", "HR", "Accounting" } // Changed
            }
        };

        return new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "COMPANY_ADMIN",
            ActorId = 456,
            CompanyId = 1,
            BranchId = null, // Company-level change
            Action = "UPDATE",
            EntityType = "Company",
            EntityId = 1,
            IpAddress = "203.0.113.10",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = DateTime.UtcNow,
            
            // Complex object serialization
            OldValue = JsonSerializer.Serialize(oldCompany, new JsonSerializerOptions { WriteIndented = true }),
            NewValue = JsonSerializer.Serialize(newCompany, new JsonSerializerOptions { WriteIndented = true }),
            
            // Track changed fields including nested properties
            ChangedFields = new Dictionary<string, object>
            {
                { "Name", "Acme Corporation" },
                { "Address.Street", "456 Broadway" },
                { "Settings.TimeZone", "PST" },
                { "Settings.Features", new[] { "POS", "HR", "Accounting" } }
            }
        };
    }

    /// <summary>
    /// Example of handling sensitive data masking in audit events
    /// </summary>
    public static DataChangeAuditEvent CreateSensitiveDataAuditEvent()
    {
        var oldUser = new 
        { 
            Id = 123,
            Username = "john.doe",
            Email = "john.doe@example.com",
            Password = "***MASKED***", // Sensitive data masked
            CreditCard = "***MASKED***", // Sensitive data masked
            SSN = "***MASKED***" // Sensitive data masked
        };

        var newUser = new 
        { 
            Id = 123,
            Username = "john.smith",
            Email = "john.smith@example.com", 
            Password = "***MASKED***", // Sensitive data masked
            CreditCard = "***MASKED***", // Sensitive data masked
            SSN = "***MASKED***" // Sensitive data masked
        };

        return new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "UPDATE",
            EntityType = "User",
            EntityId = 123,
            IpAddress = "192.168.1.200",
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_7_1 like Mac OS X)",
            Timestamp = DateTime.UtcNow,
            
            // Sensitive data is masked before storing
            OldValue = JsonSerializer.Serialize(oldUser),
            NewValue = JsonSerializer.Serialize(newUser),
            
            // Only non-sensitive fields are tracked in ChangedFields
            ChangedFields = new Dictionary<string, object>
            {
                { "Username", "john.smith" },
                { "Email", "john.smith@example.com" }
                // Password, CreditCard, SSN changes are not tracked for security
            }
        };
    }
}