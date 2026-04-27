using System.Text.Json;
using ThinkOnErp.Domain.Entities.Audit;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for PermissionChangeAuditEvent class
/// </summary>
public class PermissionChangeAuditEventTests
{
    [Fact]
    public void PermissionChangeAuditEvent_Inherits_From_AuditEvent()
    {
        // Arrange & Act
        var auditEvent = new PermissionChangeAuditEvent();
        
        // Assert
        Assert.IsAssignableFrom<AuditEvent>(auditEvent);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Role_Assignment_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var permissionBefore = JsonSerializer.Serialize(new { HasRole = false, RoleId = (long?)null });
        var permissionAfter = JsonSerializer.Serialize(new { HasRole = true, RoleId = 5L });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "ROLE_ASSIGNED",
            EntityType = "User",
            EntityId = 456,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = 5,
            PermissionId = null,
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("ROLE_ASSIGNED", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(456, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal(5, auditEvent.RoleId);
        Assert.Null(auditEvent.PermissionId);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Role_Revocation_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var permissionBefore = JsonSerializer.Serialize(new { HasRole = true, RoleId = 5L });
        var permissionAfter = JsonSerializer.Serialize(new { HasRole = false, RoleId = (long?)null });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "ROLE_REVOKED",
            EntityType = "User",
            EntityId = 456,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = 5,
            PermissionId = null,
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("ROLE_REVOKED", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(456, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal(5, auditEvent.RoleId);
        Assert.Null(auditEvent.PermissionId);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Permission_Grant_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var permissionBefore = JsonSerializer.Serialize(new { HasPermission = false, PermissionId = (long?)null });
        var permissionAfter = JsonSerializer.Serialize(new { HasPermission = true, PermissionId = 10L });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 1,
            BranchId = null, // Role permissions are typically company-level
            Action = "PERMISSION_GRANTED",
            EntityType = "Role",
            EntityId = 5,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = 5,
            PermissionId = 10,
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Null(auditEvent.BranchId);
        Assert.Equal("PERMISSION_GRANTED", auditEvent.Action);
        Assert.Equal("Role", auditEvent.EntityType);
        Assert.Equal(5, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal(5, auditEvent.RoleId);
        Assert.Equal(10, auditEvent.PermissionId);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Permission_Revocation_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var permissionBefore = JsonSerializer.Serialize(new { HasPermission = true, PermissionId = 10L });
        var permissionAfter = JsonSerializer.Serialize(new { HasPermission = false, PermissionId = (long?)null });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 1,
            BranchId = null,
            Action = "PERMISSION_REVOKED",
            EntityType = "Role",
            EntityId = 5,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = 5,
            PermissionId = 10,
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Null(auditEvent.BranchId);
        Assert.Equal("PERMISSION_REVOKED", auditEvent.Action);
        Assert.Equal("Role", auditEvent.EntityType);
        Assert.Equal(5, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal(5, auditEvent.RoleId);
        Assert.Equal(10, auditEvent.PermissionId);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Bulk_Permission_Changes()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var oldPermissions = new long[] { 1, 2, 3 };
        var newPermissions = new long[] { 1, 2, 3, 4, 5 };
        var permissionBefore = JsonSerializer.Serialize(new { Permissions = oldPermissions });
        var permissionAfter = JsonSerializer.Serialize(new { Permissions = newPermissions });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 1,
            BranchId = null,
            Action = "BULK_PERMISSIONS_GRANTED",
            EntityType = "Role",
            EntityId = 5,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = 5,
            PermissionId = null, // Multiple permissions, stored in JSON
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Null(auditEvent.BranchId);
        Assert.Equal("BULK_PERMISSIONS_GRANTED", auditEvent.Action);
        Assert.Equal("Role", auditEvent.EntityType);
        Assert.Equal(5, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Equal(5, auditEvent.RoleId);
        Assert.Null(auditEvent.PermissionId);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Permission_Query_Tracking()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var queryDetails = JsonSerializer.Serialize(new { 
            Action = "QUERY_USER_PERMISSIONS", 
            TargetUserId = 456L,
            QueryTime = timestamp 
        });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "PERMISSION_QUERY",
            EntityType = "User",
            EntityId = 456,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = null,
            PermissionId = null,
            PermissionBefore = null, // Not applicable for queries
            PermissionAfter = queryDetails // Store query details
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("PERMISSION_QUERY", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(456, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Null(auditEvent.RoleId);
        Assert.Null(auditEvent.PermissionId);
        Assert.Null(auditEvent.PermissionBefore);
        Assert.Equal(queryDetails, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Super_Admin_Privilege_Escalation()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var escalationReason = "Emergency system maintenance";
        var permissionBefore = JsonSerializer.Serialize(new { 
            IsSuperAdmin = false, 
            EscalationReason = (string?)null 
        });
        var permissionAfter = JsonSerializer.Serialize(new { 
            IsSuperAdmin = true, 
            EscalationReason = escalationReason,
            EscalatedBy = 999L,
            EscalationTime = timestamp
        });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "SUPER_ADMIN",
            ActorId = 999,
            CompanyId = 1,
            BranchId = null, // Super admin actions are company-level
            Action = "PRIVILEGE_ESCALATION",
            EntityType = "User",
            EntityId = 456,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = null, // Not role-based escalation
            PermissionId = null, // Not specific permission
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("SUPER_ADMIN", auditEvent.ActorType);
        Assert.Equal(999, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Null(auditEvent.BranchId);
        Assert.Equal("PRIVILEGE_ESCALATION", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(456, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Null(auditEvent.RoleId);
        Assert.Null(auditEvent.PermissionId);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Can_Handle_Null_Optional_Properties()
    {
        // Arrange & Act
        var auditEvent = new PermissionChangeAuditEvent
        {
            RoleId = null,
            PermissionId = null,
            PermissionBefore = null,
            PermissionAfter = null
        };

        // Assert
        Assert.Null(auditEvent.RoleId);
        Assert.Null(auditEvent.PermissionId);
        Assert.Null(auditEvent.PermissionBefore);
        Assert.Null(auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Can_Handle_Various_Action_Types()
    {
        // Arrange
        var actionTypes = new[]
        {
            "ROLE_ASSIGNED",
            "ROLE_REVOKED",
            "PERMISSION_GRANTED",
            "PERMISSION_REVOKED",
            "BULK_PERMISSIONS_GRANTED",
            "BULK_PERMISSIONS_REVOKED",
            "PERMISSION_QUERY",
            "PRIVILEGE_ESCALATION",
            "ROLE_ASSIGNMENT_CHANGED"
        };

        foreach (var actionType in actionTypes)
        {
            // Act
            var auditEvent = new PermissionChangeAuditEvent
            {
                Action = actionType
            };

            // Assert
            Assert.Equal(actionType, auditEvent.Action);
        }
    }

    [Fact]
    public void PermissionChangeAuditEvent_Can_Handle_Various_Entity_Types()
    {
        // Arrange
        var entityTypes = new[]
        {
            "User",
            "Role",
            "Permission",
            "Company",
            "Branch"
        };

        foreach (var entityType in entityTypes)
        {
            // Act
            var auditEvent = new PermissionChangeAuditEvent
            {
                EntityType = entityType
            };

            // Assert
            Assert.Equal(entityType, auditEvent.EntityType);
        }
    }

    [Fact]
    public void PermissionChangeAuditEvent_Can_Handle_Various_Actor_Types()
    {
        // Arrange
        var actorTypes = new[]
        {
            "SUPER_ADMIN",
            "COMPANY_ADMIN",
            "USER",
            "SYSTEM"
        };

        foreach (var actorType in actorTypes)
        {
            // Act
            var auditEvent = new PermissionChangeAuditEvent
            {
                ActorType = actorType
            };

            // Assert
            Assert.Equal(actorType, auditEvent.ActorType);
        }
    }

    [Fact]
    public void PermissionChangeAuditEvent_JSON_Serialization_Deserialization()
    {
        // Arrange
        var originalPermissions = new { RoleIds = new[] { 1L, 2L, 3L }, Permissions = new[] { "READ", "WRITE", "DELETE" } };
        var modifiedPermissions = new { RoleIds = new[] { 1L, 2L }, Permissions = new[] { "READ", "WRITE" } };
        
        var permissionBefore = JsonSerializer.Serialize(originalPermissions);
        var permissionAfter = JsonSerializer.Serialize(modifiedPermissions);
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act
        var deserializedBefore = JsonSerializer.Deserialize<dynamic>(auditEvent.PermissionBefore!);
        var deserializedAfter = JsonSerializer.Deserialize<dynamic>(auditEvent.PermissionAfter!);

        // Assert
        Assert.NotNull(deserializedBefore);
        Assert.NotNull(deserializedAfter);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Inherits_All_Base_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 456,
            CompanyId = 2,
            BranchId = 3,
            Action = "ROLE_ASSIGNED",
            EntityType = "User",
            EntityId = 789,
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            Timestamp = timestamp
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(456, auditEvent.ActorId);
        Assert.Equal(2, auditEvent.CompanyId);
        Assert.Equal(3, auditEvent.BranchId);
        Assert.Equal("ROLE_ASSIGNED", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(789, auditEvent.EntityId);
        Assert.Equal("10.0.0.1", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Complex_Permission_State_Changes()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var oldRoleIds = new[] { 1L, 2L };
        var newRoleIds = new[] { 2L, 3L, 4L };
        
        var permissionBefore = JsonSerializer.Serialize(new { 
            RoleIds = oldRoleIds,
            EffectivePermissions = new[] { "READ_USERS", "READ_COMPANIES" },
            LastModified = timestamp.AddMinutes(-5)
        });
        var permissionAfter = JsonSerializer.Serialize(new { 
            RoleIds = newRoleIds,
            EffectivePermissions = new[] { "READ_USERS", "READ_COMPANIES", "WRITE_USERS", "ADMIN_PERMISSIONS" },
            LastModified = timestamp
        });
        
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "ROLE_ASSIGNMENT_CHANGED",
            EntityType = "User",
            EntityId = 456,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            RoleId = null, // Multiple roles involved
            PermissionId = null, // Multiple permissions involved
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("ROLE_ASSIGNMENT_CHANGED", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(456, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.Null(auditEvent.RoleId);
        Assert.Null(auditEvent.PermissionId);
        Assert.Equal(permissionBefore, auditEvent.PermissionBefore);
        Assert.Equal(permissionAfter, auditEvent.PermissionAfter);
        
        // Verify JSON content can be deserialized
        Assert.NotNull(auditEvent.PermissionBefore);
        Assert.NotNull(auditEvent.PermissionAfter);
        Assert.Contains("RoleIds", auditEvent.PermissionBefore);
        Assert.Contains("EffectivePermissions", auditEvent.PermissionBefore);
        Assert.Contains("RoleIds", auditEvent.PermissionAfter);
        Assert.Contains("EffectivePermissions", auditEvent.PermissionAfter);
    }
}