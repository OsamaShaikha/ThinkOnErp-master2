# Multi-Tenant Permissions System

## Overview

The ThinkOnErp system now includes a comprehensive multi-tenant permissions system that provides granular screen-level access control with support for Super Admin, Company Admin, and regular users.

## Architecture

### Permission Hierarchy

```
Super Admin (full platform control)
    │
    ├── Manages Systems & Screens (global)
    ├── Manages Companies (create/suspend/delete)
    ├── Assigns System-level access per Company
    └── Manages Permissions (all companies)
            │
            └── Company (Tenant)
                    └── Company Admin (manages own company only)
                            └── Company Users
```

## Database Schema

### Core Permission Tables

1. **SYS_SUPER_ADMIN** - Super admin accounts with 2FA support
2. **SYS_SYSTEM** - Available systems/modules (Accounting, Inventory, HR, CRM, POS)
3. **SYS_SCREEN** - Screens/pages within each system
4. **SYS_COMPANY_SYSTEM** - System access control per company (allow/block)
5. **SYS_ROLE_SCREEN_PERMISSION** - Screen permissions per role (View/Insert/Update/Delete)
6. **SYS_USER_ROLE** - User to role assignments (many-to-many)
7. **SYS_USER_SCREEN_PERMISSION** - Direct user permission overrides
8. **SYS_AUDIT_LOG** - Comprehensive audit trail

### Extended Existing Tables

- **SYS_USERS.IS_SUPER_ADMIN** - Flag to identify super admin users

## Permission Resolution Logic

The system uses a layered permission resolution in this order:

```
0. Is the actor a Super Admin?
   → YES → Grant access immediately (bypass all checks)

1. Is the system allowed for this company?
   → NO → Deny all access

2. Is the user active (and company active)?
   → NO → Deny

3. Check UserScreenPermission (direct override):
   → If exists → use it (highest priority)

4. Check RoleScreenPermission (merge all roles with OR logic):
   → If ANY role grants permission → allow

5. Default → Deny
```

## Database Scripts

### Installation Order

Execute these scripts in order:

1. **08_Create_Permissions_Tables.sql** - Creates all permission tables
2. **09_Create_Permissions_Sequences.sql** - Creates sequences for primary keys
3. **10_Create_Permissions_Procedures.sql** - Creates CRUD procedures and permission function
4. **11_Insert_Permissions_Seed_Data.sql** - Inserts demo systems and screens

### Key Stored Procedures

#### System Management
- `SP_SYS_SYSTEM_GET_ALL` - Get all systems
- `SP_SYS_SYSTEM_GET_BY_ID` - Get system by ID
- `SP_SYS_SYSTEM_CREATE` - Create new system
- `SP_SYS_SYSTEM_UPDATE` - Update system
- `SP_SYS_SYSTEM_DELETE` - Soft delete system

#### Screen Management
- `SP_SYS_SCREEN_GET_ALL` - Get all screens
- `SP_SYS_SCREEN_GET_BY_SYSTEM` - Get screens by system ID
- `SP_SYS_SCREEN_GET_BY_ID` - Get screen by ID
- `SP_SYS_SCREEN_CREATE` - Create new screen
- `SP_SYS_SCREEN_UPDATE` - Update screen
- `SP_SYS_SCREEN_DELETE` - Soft delete screen

#### Company System Assignment
- `SP_SYS_COMPANY_SYSTEM_GET` - Get company's system assignments
- `SP_SYS_COMPANY_SYSTEM_SET` - Allow/block system for company

#### Role Screen Permissions
- `SP_SYS_ROLE_SCREEN_PERM_GET` - Get role's screen permissions
- `SP_SYS_ROLE_SCREEN_PERM_SET` - Set role screen permission
- `SP_SYS_ROLE_SCREEN_PERM_DEL` - Delete role screen permission

#### User Role Assignments
- `SP_SYS_USER_ROLE_GET` - Get user's roles
- `SP_SYS_USER_ROLE_ASSIGN` - Assign role to user
- `SP_SYS_USER_ROLE_REMOVE` - Remove role from user

#### User Screen Permission Overrides
- `SP_SYS_USER_SCREEN_PERM_GET` - Get user's permission overrides
- `SP_SYS_USER_SCREEN_PERM_SET` - Set user permission override
- `SP_SYS_USER_SCREEN_PERM_DEL` - Delete user permission override

#### Permission Check Function
- `FN_CHECK_USER_PERMISSION(userId, screenCode, action)` - Returns '1' if allowed, '0' if denied
  - Actions: 'VIEW', 'INSERT', 'UPDATE', 'DELETE'
  - Super Admin always returns '1'
  - Implements full permission resolution logic

## Demo Data

The seed data script includes:

### Systems (5 total)
1. **Accounting** - Chart of Accounts, Journal Entries, Invoices, Payments, Financial Reports
2. **Inventory** - Products, Warehouses, Stock Movements, Purchase Orders, Stock Reports
3. **HR** - Employees, Payroll, Leave Requests, Attendance, HR Reports
4. **CRM** - Customers, Leads, Opportunities, Sales Reports
5. **POS** - Point of Sale, Daily Sales, Cash Drawer, POS Reports

### Screens (24 total)
Each system has 4-5 screens with proper routing and display order.

### Demo Company Assignments
- Company 1: Allowed (Accounting, Inventory, CRM), Blocked (HR, POS)
- Company 2: Allowed (Accounting, POS), Blocked (Inventory, HR, CRM)

## Implementation Status

### Phase 1: Database Schema ✅ COMPLETE
- [x] Create permission tables
- [x] Create sequences
- [x] Create stored procedures
- [x] Create seed data with demo systems and screens
- [x] Create permission resolution function

### Phase 2: C# Entities ✅ COMPLETE
- [x] Create entity classes for new tables (6 entities)
- [x] Create DTOs for API requests/responses (10 DTOs)
- [x] Create repository interfaces (3 interfaces)
- [x] Implement repositories with ADO.NET (3 repositories)
- [x] Register repositories in DependencyInjection

### Phase 3: CQRS Handlers ✅ COMPLETE
- [x] Create query handlers (5 queries)
- [x] Create command handlers (5 commands)
- [x] Implement permission check query
- [x] Implement role/user management commands

### Phase 4: API Endpoints ✅ COMPLETE
- [x] Create PermissionsController with all endpoints
- [x] Permission check API
- [x] System and screen management APIs
- [x] User role assignment APIs
- [x] Role screen permission APIs
- [x] User screen permission override APIs
- [x] Company system assignment APIs

### Phase 5: Advanced Features (NEXT)
- [ ] Create permission check middleware
- [ ] Add caching layer (Redis)
- [ ] Create permission service wrapper
- [ ] Add Super Admin management endpoints
- [ ] Create audit log viewer

### Phase 6: Frontend Integration (PENDING)
- [ ] Permission hooks (usePermission)
- [ ] Route guards
- [ ] Conditional UI rendering
- [ ] Permission matrix UI component

## Usage Examples

### Check User Permission (SQL)

```sql
-- Check if user 123 can view invoices
SELECT FN_CHECK_USER_PERMISSION(123, 'invoices', 'VIEW') FROM DUAL;

-- Check if user 123 can insert products
SELECT FN_CHECK_USER_PERMISSION(123, 'products', 'INSERT') FROM DUAL;
```

### Assign System to Company

```sql
-- Allow Accounting system for Company 1
EXEC SP_SYS_COMPANY_SYSTEM_SET(
    P_COMPANY_ID => 1,
    P_SYSTEM_ID => 1,
    P_IS_ALLOWED => '1',
    P_GRANTED_BY => NULL,
    P_NOTES => 'Initial setup',
    P_CREATION_USER => 'admin'
);
```

### Set Role Screen Permission

```sql
-- Give full permissions to role 7 for invoices screen
EXEC SP_SYS_ROLE_SCREEN_PERM_SET(
    P_ROLE_ID => 7,
    P_SCREEN_ID => 3,
    P_CAN_VIEW => '1',
    P_CAN_INSERT => '1',
    P_CAN_UPDATE => '1',
    P_CAN_DELETE => '1',
    P_CREATION_USER => 'admin'
);
```

### Assign Role to User

```sql
-- Assign role 7 to user 0
EXEC SP_SYS_USER_ROLE_ASSIGN(
    P_USER_ID => 0,
    P_ROLE_ID => 7,
    P_ASSIGNED_BY => NULL,
    P_CREATION_USER => 'admin'
);
```

## Security Features

1. **Super Admin Bypass** - Super admins have unrestricted access
2. **System-Level Control** - Block entire systems per company
3. **Role-Based Permissions** - Granular screen-level permissions per role
4. **User Overrides** - Direct user permissions override role permissions
5. **OR Logic** - Users with multiple roles get combined permissions
6. **Audit Trail** - All permission changes logged in SYS_AUDIT_LOG
7. **Soft Deletes** - Systems and screens use IS_ACTIVE flag

## Performance Considerations

1. **Indexes** - All foreign keys and lookup columns are indexed
2. **Caching** - Permission resolution should be cached (Redis recommended)
3. **Function-Based** - Permission check is a single function call
4. **Optimized Queries** - Uses MAX() for OR logic across roles

## API Endpoints

The following endpoints are now available in the PermissionsController:

### Permission Check
```
POST /api/permissions/check
Body: { userId, screenCode, action }
Response: { allowed: true/false, reason?: string }
```

### Systems & Screens
```
GET  /api/permissions/systems
GET  /api/permissions/systems/{systemId}/screens
```

### User Roles
```
GET    /api/permissions/users/{userId}/roles
POST   /api/permissions/users/{userId}/roles/{roleId}    [Admin Only]
DELETE /api/permissions/users/{userId}/roles/{roleId}    [Admin Only]
```

### Role Screen Permissions
```
GET /api/permissions/roles/{roleId}/permissions
PUT /api/permissions/roles/{roleId}/permissions          [Admin Only]
Body: { screenId, canView, canInsert, canUpdate, canDelete }
```

### User Screen Permission Overrides
```
PUT /api/permissions/users/{userId}/permissions          [Admin Only]
Body: { screenId, canView, canInsert, canUpdate, canDelete, notes }
```

### Company System Assignments
```
PUT /api/permissions/companies/{companyId}/systems/{systemId}?isAllowed=true  [Admin Only]
```

## Next Steps

1. Create C# entity classes for all permission tables
2. Implement repositories with stored procedure calls
3. Create permission resolution service in C#
4. Add permission check middleware to API
5. Implement Super Admin and Company Admin APIs
6. Add frontend permission hooks and guards

## References

- Specification: `.kiro/kiro-permissions-system-prompt (1).md`
- Database Scripts: `Database/Scripts/08-11_*.sql`
- Test Data: `Database/Scripts/06_Insert_Test_Data.sql`
