# Branch-Level Permissions System Guide

## Overview

This system allows **Super Admins** to control access to modules (systems) and screens at the **branch level**. This provides granular control over which features each branch can access within a company.

## Database Tables

### 1. SYS_BRANCH_SYSTEM
Controls which **systems/modules** (e.g., Accounting, Inventory, HR) are accessible per branch.

**Key Fields:**
- `BRANCH_ID`: The branch this permission applies to
- `SYSTEM_ID`: The system/module (from SYS_SYSTEM table)
- `IS_ALLOWED`: '1' = Allowed, '0' = Blocked
- `GRANTED_BY`: Super Admin who granted/revoked access
- `GRANTED_DATE`: When access was granted
- `REVOKED_DATE`: When access was revoked (if applicable)

### 2. SYS_BRANCH_SCREEN
Controls which **screens** (e.g., invoices_list, customers_create) are accessible per branch.

**Key Fields:**
- `BRANCH_ID`: The branch this permission applies to
- `SCREEN_ID`: The screen (from SYS_SCREEN table)
- `IS_ALLOWED`: '1' = Allowed, '0' = Blocked
- `GRANTED_BY`: Super Admin who granted/revoked access
- `GRANTED_DATE`: When access was granted
- `REVOKED_DATE`: When access was revoked (if applicable)

## Permission Hierarchy

The system follows this permission hierarchy (from highest to lowest priority):

```
1. Company Level (SYS_COMPANY_SYSTEM)
   ↓
2. Branch Level (SYS_BRANCH_SYSTEM, SYS_BRANCH_SCREEN) ← NEW
   ↓
3. Role Level (SYS_ROLE_SCREEN_PERMISSION)
   ↓
4. User Level (SYS_USER_SCREEN_PERMISSION)
```

### Permission Resolution Logic

1. **Company Level**: If a system is blocked at company level, it's blocked for all branches
2. **Branch Level**: If a system is blocked at branch level, it's blocked for all users in that branch
3. **Screen Level**: If a screen is blocked at branch level, it's blocked for all users in that branch
4. **Role/User Level**: Standard role-based and user-specific permissions apply within allowed systems/screens

## Use Cases

### Use Case 1: Restrict Accounting Module to Headquarters Only

```sql
-- Block Accounting system for Branch 2
INSERT INTO SYS_BRANCH_SYSTEM (
    ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED, 
    GRANTED_BY, CREATION_USER
) VALUES (
    SEQ_SYS_BRANCH_SYSTEM.NEXTVAL, 
    2,  -- Branch ID
    1,  -- Accounting System ID
    '0',  -- Blocked
    1,  -- Super Admin ID
    'superadmin'
);
```

### Use Case 2: Allow Only Invoice Viewing for Remote Branch

```sql
-- Allow Accounting system
INSERT INTO SYS_BRANCH_SYSTEM (
    ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED, 
    GRANTED_BY, CREATION_USER
) VALUES (
    SEQ_SYS_BRANCH_SYSTEM.NEXTVAL, 
    3,  -- Remote Branch ID
    1,  -- Accounting System ID
    '1',  -- Allowed
    1,  -- Super Admin ID
    'superadmin'
);

-- Block invoice creation screen
INSERT INTO SYS_BRANCH_SCREEN (
    ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED, 
    GRANTED_BY, CREATION_USER
) VALUES (
    SEQ_SYS_BRANCH_SCREEN.NEXTVAL, 
    3,  -- Remote Branch ID
    15,  -- Invoice Create Screen ID
    '0',  -- Blocked
    1,  -- Super Admin ID
    'superadmin'
);
```

### Use Case 3: Grant Full Access to Main Branch

```sql
-- Allow all systems for main branch (no restrictions)
-- Simply don't insert any blocking records for the main branch
-- Or explicitly grant access to all systems
```

## API Endpoints Needed

### Super Admin Endpoints

#### 1. Grant/Revoke System Access to Branch
```
POST /api/superadmin/branches/{branchId}/systems/{systemId}/grant
POST /api/superadmin/branches/{branchId}/systems/{systemId}/revoke
```

#### 2. Grant/Revoke Screen Access to Branch
```
POST /api/superadmin/branches/{branchId}/screens/{screenId}/grant
POST /api/superadmin/branches/{branchId}/screens/{screenId}/revoke
```

#### 3. Get Branch Permissions
```
GET /api/superadmin/branches/{branchId}/systems
GET /api/superadmin/branches/{branchId}/screens
```

#### 4. Get All Branches with Permissions
```
GET /api/superadmin/branches/permissions
```

## Implementation Steps

### Phase 1: Database Setup
1. ✅ Run `Database/Scripts/88_Create_Branch_Level_Permissions.sql`
2. Verify tables created successfully
3. Create test data for systems and screens

### Phase 2: Domain Layer
1. Create `SysBranchSystem` entity
2. Create `SysBranchScreen` entity
3. Create repository interfaces

### Phase 3: Infrastructure Layer
1. Implement `BranchSystemRepository`
2. Implement `BranchScreenRepository`
3. Create stored procedures for CRUD operations

### Phase 4: Application Layer
1. Create DTOs for branch permissions
2. Create MediatR commands/queries
3. Implement command/query handlers
4. Add validation logic

### Phase 5: API Layer
1. Create `BranchPermissionsController` (Super Admin only)
2. Implement endpoints for grant/revoke operations
3. Add authorization policies
4. Add audit logging

### Phase 6: Permission Checking Middleware
1. Create middleware to check branch-level permissions
2. Integrate with existing permission checking logic
3. Update JWT token to include branch information
4. Cache branch permissions for performance

## Security Considerations

1. **Super Admin Only**: Only super admins can manage branch-level permissions
2. **Audit Trail**: All permission changes are logged in SYS_AUDIT_LOG
3. **Cascade Blocking**: If a system is blocked, all its screens are automatically inaccessible
4. **No Self-Revocation**: Super admins cannot revoke their own access
5. **Company Boundary**: Branch permissions cannot override company-level restrictions

## Performance Optimization

1. **Caching**: Cache branch permissions in Redis/Memory
2. **Indexes**: Created on BRANCH_ID, SYSTEM_ID, SCREEN_ID for fast lookups
3. **Batch Operations**: Support bulk grant/revoke operations
4. **Lazy Loading**: Load permissions only when needed

## Testing Strategy

### Unit Tests
- Test permission resolution logic
- Test grant/revoke operations
- Test validation rules

### Integration Tests
- Test database operations
- Test API endpoints
- Test permission checking middleware

### End-to-End Tests
- Test super admin granting access
- Test user accessing allowed/blocked features
- Test permission hierarchy

## Migration Path

### For Existing Systems
1. **Default Behavior**: If no branch-level permissions exist, allow all (backward compatible)
2. **Gradual Rollout**: Start with one branch, test, then expand
3. **Rollback Plan**: Drop tables if needed (no impact on existing functionality)

## Next Steps

1. Review and approve database script
2. Execute script on development database
3. Create domain entities and repositories
4. Implement API endpoints
5. Add permission checking logic
6. Test with sample data
7. Deploy to staging
8. Train super admins on new feature
9. Deploy to production

## Questions?

Contact the development team for any questions or clarifications about this feature.
