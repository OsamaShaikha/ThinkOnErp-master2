using System;
using System.Text.Json;
using ThinkOnErp.Domain.Entities.Audit;

namespace ThinkOnErp.Examples;

/// <summary>
/// Example usage of PermissionChangeAuditEvent class for different permission change scenarios
/// </summary>
public class PermissionChangeAuditEventUsageExample
{
    /// <summary>
    /// Example: Role assigned to a user
    /// </summary>
    public static PermissionChangeAuditEvent CreateRoleAssignmentEvent(long adminUserId, long targetUserId, long roleId, long companyId, long branchId, string ipAddress, string userAgent, string correlationId)
    {
        var permissionBefore = JsonSerializer.Serialize(new { HasRole = false, RoleId = (long?)null });
        var permissionAfter = JsonSerializer.Serialize(new { HasRole = true, RoleId = roleId });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = adminUserId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "ROLE_ASSIGNED",
            EntityType = "User",
            EntityId = targetUserId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = roleId,
            PermissionId = null, // Not applicable for role assignment
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };
    }

    /// <summary>
    /// Example: Role revoked from a user
    /// </summary>
    public static PermissionChangeAuditEvent CreateRoleRevocationEvent(long adminUserId, long targetUserId, long roleId, long companyId, long branchId, string ipAddress, string userAgent, string correlationId)
    {
        var permissionBefore = JsonSerializer.Serialize(new { HasRole = true, RoleId = roleId });
        var permissionAfter = JsonSerializer.Serialize(new { HasRole = false, RoleId = (long?)null });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = adminUserId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "ROLE_REVOKED",
            EntityType = "User",
            EntityId = targetUserId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = roleId,
            PermissionId = null,
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };
    }

    /// <summary>
    /// Example: Permission granted to a role
    /// </summary>
    public static PermissionChangeAuditEvent CreatePermissionGrantEvent(long adminUserId, long roleId, long permissionId, long companyId, string ipAddress, string userAgent, string correlationId)
    {
        var permissionBefore = JsonSerializer.Serialize(new { HasPermission = false, PermissionId = (long?)null });
        var permissionAfter = JsonSerializer.Serialize(new { HasPermission = true, PermissionId = permissionId });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = adminUserId,
            CompanyId = companyId,
            BranchId = null, // Role permissions are typically company-level
            Action = "PERMISSION_GRANTED",
            EntityType = "Role",
            EntityId = roleId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = roleId,
            PermissionId = permissionId,
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };
    }

    /// <summary>
    /// Example: Permission revoked from a role
    /// </summary>
    public static PermissionChangeAuditEvent CreatePermissionRevocationEvent(long adminUserId, long roleId, long permissionId, long companyId, string ipAddress, string userAgent, string correlationId)
    {
        var permissionBefore = JsonSerializer.Serialize(new { HasPermission = true, PermissionId = permissionId });
        var permissionAfter = JsonSerializer.Serialize(new { HasPermission = false, PermissionId = (long?)null });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = adminUserId,
            CompanyId = companyId,
            BranchId = null,
            Action = "PERMISSION_REVOKED",
            EntityType = "Role",
            EntityId = roleId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = roleId,
            PermissionId = permissionId,
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };
    }

    /// <summary>
    /// Example: Bulk permission changes (multiple permissions granted to a role)
    /// </summary>
    public static PermissionChangeAuditEvent CreateBulkPermissionGrantEvent(long adminUserId, long roleId, long[] permissionIds, long companyId, string ipAddress, string userAgent, string correlationId)
    {
        var permissionBefore = JsonSerializer.Serialize(new { Permissions = new long[0] });
        var permissionAfter = JsonSerializer.Serialize(new { Permissions = permissionIds });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = adminUserId,
            CompanyId = companyId,
            BranchId = null,
            Action = "BULK_PERMISSIONS_GRANTED",
            EntityType = "Role",
            EntityId = roleId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = roleId,
            PermissionId = null, // Multiple permissions, stored in JSON
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };
    }

    /// <summary>
    /// Example: User permission query (for compliance tracking)
    /// </summary>
    public static PermissionChangeAuditEvent CreatePermissionQueryEvent(long queryingUserId, long targetUserId, long companyId, long branchId, string ipAddress, string userAgent, string correlationId)
    {
        var currentPermissions = JsonSerializer.Serialize(new { 
            Action = "QUERY_USER_PERMISSIONS", 
            TargetUserId = targetUserId,
            QueryTime = DateTime.UtcNow 
        });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = queryingUserId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "PERMISSION_QUERY",
            EntityType = "User",
            EntityId = targetUserId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = null,
            PermissionId = null,
            PermissionBefore = null, // Not applicable for queries
            PermissionAfter = currentPermissions // Store query details
        };
    }

    /// <summary>
    /// Example: Super admin privilege escalation
    /// </summary>
    public static PermissionChangeAuditEvent CreatePrivilegeEscalationEvent(long superAdminId, long targetUserId, string escalationReason, long companyId, string ipAddress, string userAgent, string correlationId)
    {
        var permissionBefore = JsonSerializer.Serialize(new { 
            IsSuperAdmin = false, 
            EscalationReason = (string?)null 
        });
        var permissionAfter = JsonSerializer.Serialize(new { 
            IsSuperAdmin = true, 
            EscalationReason = escalationReason,
            EscalatedBy = superAdminId,
            EscalationTime = DateTime.UtcNow
        });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "SUPER_ADMIN",
            ActorId = superAdminId,
            CompanyId = companyId,
            BranchId = null, // Super admin actions are company-level
            Action = "PRIVILEGE_ESCALATION",
            EntityType = "User",
            EntityId = targetUserId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = null, // Not role-based escalation
            PermissionId = null, // Not specific permission
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };
    }

    /// <summary>
    /// Example: Complex permission state change with multiple roles
    /// </summary>
    public static PermissionChangeAuditEvent CreateComplexPermissionChangeEvent(long adminUserId, long targetUserId, long[] oldRoleIds, long[] newRoleIds, long companyId, long branchId, string ipAddress, string userAgent, string correlationId)
    {
        var permissionBefore = JsonSerializer.Serialize(new { 
            RoleIds = oldRoleIds,
            EffectivePermissions = GetEffectivePermissions(oldRoleIds),
            LastModified = DateTime.UtcNow.AddMinutes(-5)
        });
        var permissionAfter = JsonSerializer.Serialize(new { 
            RoleIds = newRoleIds,
            EffectivePermissions = GetEffectivePermissions(newRoleIds),
            LastModified = DateTime.UtcNow
        });

        return new PermissionChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = adminUserId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "ROLE_ASSIGNMENT_CHANGED",
            EntityType = "User",
            EntityId = targetUserId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Permission-specific properties
            RoleId = null, // Multiple roles involved
            PermissionId = null, // Multiple permissions involved
            PermissionBefore = permissionBefore,
            PermissionAfter = permissionAfter
        };
    }

    /// <summary>
    /// Helper method to simulate getting effective permissions for roles
    /// </summary>
    private static string[] GetEffectivePermissions(long[] roleIds)
    {
        // This would typically query the database to get actual permissions
        // For example purposes, we'll return mock permissions
        return roleIds.Length switch
        {
            0 => new string[0],
            1 => new[] { "READ_USERS", "READ_COMPANIES" },
            2 => new[] { "READ_USERS", "READ_COMPANIES", "WRITE_USERS" },
            _ => new[] { "READ_USERS", "READ_COMPANIES", "WRITE_USERS", "ADMIN_PERMISSIONS" }
        };
    }

    /// <summary>
    /// Common action types for permission change events
    /// </summary>
    public static class ActionTypes
    {
        public const string RoleAssigned = "ROLE_ASSIGNED";
        public const string RoleRevoked = "ROLE_REVOKED";
        public const string PermissionGranted = "PERMISSION_GRANTED";
        public const string PermissionRevoked = "PERMISSION_REVOKED";
        public const string BulkPermissionsGranted = "BULK_PERMISSIONS_GRANTED";
        public const string BulkPermissionsRevoked = "BULK_PERMISSIONS_REVOKED";
        public const string PermissionQuery = "PERMISSION_QUERY";
        public const string PrivilegeEscalation = "PRIVILEGE_ESCALATION";
        public const string RoleAssignmentChanged = "ROLE_ASSIGNMENT_CHANGED";
        public const string PermissionInheritanceChanged = "PERMISSION_INHERITANCE_CHANGED";
    }

    /// <summary>
    /// Common entity types for permission change events
    /// </summary>
    public static class EntityTypes
    {
        public const string User = "User";
        public const string Role = "Role";
        public const string Permission = "Permission";
        public const string Company = "Company";
        public const string Branch = "Branch";
    }

    /// <summary>
    /// Common actor types for permission change events
    /// </summary>
    public static class ActorTypes
    {
        public const string SuperAdmin = "SUPER_ADMIN";
        public const string CompanyAdmin = "COMPANY_ADMIN";
        public const string User = "USER";
        public const string System = "SYSTEM";
    }
}