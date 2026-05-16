# Branch Permissions SQL Scripts - Documentation

## Overview
This documentation covers the SQL scripts for managing branch-level permissions in the ThinkOnErp system. These scripts provide database layer support for granting/revoking system and screen access to branches.

## Files Created

### 1. **89_Create_Branch_Permissions_Procedures.sql**
Contains all stored procedures for branch permissions CRUD operations.

#### Procedures Included:

**Query Procedures:**
- `SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH` - Get all systems for a branch
- `SP_BRANCH_SCREENS_SELECT_BY_BRANCH` - Get all screens for a branch
- `SP_GET_ALL_SYSTEMS` - Get all available systems
- `SP_GET_ALL_SCREENS` - Get all available screens
- `SP_GET_SCREENS_BY_SYSTEM` - Get screens for a specific system
- `SP_CHECK_BRANCH_SYSTEM_ALLOWED` - Verify branch system access
- `SP_CHECK_BRANCH_SCREEN_ALLOWED` - Verify branch screen access

**Modification Procedures:**
- `SP_BRANCH_SYSTEM_GRANT` - Grant system access to branch
- `SP_BRANCH_SYSTEM_REVOKE` - Revoke system access from branch
- `SP_BRANCH_SCREEN_GRANT` - Grant screen access to branch
- `SP_BRANCH_SCREEN_REVOKE` - Revoke screen access from branch

### 2. **90_Sample_Branch_Permissions_Data.sql**
Contains sample/test data for branch permissions.

**Includes:**
- Sample system permissions for branches
- Sample screen permissions for branches
- Example of revoked permissions
- Verification queries (commented out)

### 3. **91_Branch_Permissions_Query_Utilities.sql**
Contains useful utility queries for reporting and analysis.

**Utilities:**
1. View all branch systems with details
2. View all branch screens with details
3. Find branches without access to specific systems
4. Permission audit trail for a branch
5. Find all allowed systems for a branch
6. Find all allowed screens for a branch
7. System access summary
8. Recent permission changes (last 30 days)
9. Permission integrity validation
10. Permission statistics by branch

## Database Tables Used

### SYS_BRANCH_SYSTEM
```
ROW_ID          NUMBER(19) PRIMARY KEY
BRANCH_ID       NUMBER(19) - FK to SYS_BRANCH
SYSTEM_ID       NUMBER(19) - FK to SYS_SYSTEM
IS_ALLOWED      CHAR(1) - '1' for allowed, '0' for blocked
GRANTED_BY      NUMBER(19) - FK to SYS_SUPER_ADMIN
GRANTED_DATE    DATE
REVOKED_DATE    DATE
NOTES           NVARCHAR2(1000)
CREATION_USER   NVARCHAR2(100)
CREATION_DATE   DATE
UPDATE_USER     NVARCHAR2(100)
UPDATE_DATE     DATE
```

**Indexes:**
- IDX_BRANCH_SYSTEM_BRANCH
- IDX_BRANCH_SYSTEM_SYSTEM
- IDX_BRANCH_SYSTEM_GRANTED_BY

### SYS_BRANCH_SCREEN
```
ROW_ID          NUMBER(19) PRIMARY KEY
BRANCH_ID       NUMBER(19) - FK to SYS_BRANCH
SCREEN_ID       NUMBER(19) - FK to SYS_SCREEN
IS_ALLOWED      CHAR(1) - '1' for allowed, '0' for blocked
GRANTED_BY      NUMBER(19) - FK to SYS_SUPER_ADMIN
GRANTED_DATE    DATE
REVOKED_DATE    DATE
NOTES           NVARCHAR2(1000)
CREATION_USER   NVARCHAR2(100)
CREATION_DATE   DATE
UPDATE_USER     NVARCHAR2(100)
UPDATE_DATE     DATE
```

**Indexes:**
- IDX_BRANCH_SCREEN_BRANCH
- IDX_BRANCH_SCREEN_SCREEN
- IDX_BRANCH_SCREEN_GRANTED_BY

## Execution Order

1. **Tables** (Already exist in database)
   - SYS_BRANCH_SYSTEM (from 88_Create_Branch_Level_Permissions.sql)
   - SYS_BRANCH_SCREEN (from 88_Create_Branch_Level_Permissions.sql)

2. **Procedures** (89_Create_Branch_Permissions_Procedures.sql)
   - Execute after table creation
   - All procedures follow naming convention: `SP_[TABLE]_[OPERATION]`

3. **Sample Data** (90_Sample_Branch_Permissions_Data.sql)
   - Execute after procedures are created
   - For testing and demonstration purposes
   - Can be skipped in production or adapted as needed

4. **Query Utilities** (91_Branch_Permissions_Query_Utilities.sql)
   - Execute anytime for reporting and analysis
   - No dependencies - read-only queries

## Usage Examples

### Grant System Access to Branch
```sql
DECLARE
	V_NEW_ID NUMBER;
BEGIN
	SP_BRANCH_SYSTEM_GRANT(
		P_BRANCH_ID => 1,           -- Branch ID
		P_SYSTEM_ID => 2,           -- System ID
		P_GRANTED_BY => 1,          -- Super Admin ID
		P_NOTES => 'Accounting system granted',
		P_CREATION_USER => 'admin',
		P_NEW_ID => V_NEW_ID
	);
	DBMS_OUTPUT.PUT_LINE('Created permission: ' || V_NEW_ID);
END;
/
```

### Revoke System Access from Branch
```sql
DECLARE
	V_UPDATED_ID NUMBER;
BEGIN
	SP_BRANCH_SYSTEM_REVOKE(
		P_BRANCH_ID => 1,           -- Branch ID
		P_SYSTEM_ID => 2,           -- System ID
		P_UPDATE_USER => 'admin',
		P_UPDATED_ID => V_UPDATED_ID
	);
	DBMS_OUTPUT.PUT_LINE('Revoked permission: ' || V_UPDATED_ID);
END;
/
```

### Get Branch Systems
```sql
SET PAGESIZE 0
SET FEEDBACK OFF
SET ECHO OFF
SET HEADING OFF
SET UNDERLINE OFF
SET LINESIZE 200

VAR ref_cursor REFCURSOR
EXEC SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH(1, :ref_cursor)
PRINT ref_cursor
```

## Key Features

✅ **Audit Trail** - All changes tracked with user and timestamp  
✅ **Soft Delete** - Revoked permissions keep historical data  
✅ **Referential Integrity** - Foreign key constraints enforced  
✅ **Performance** - Indexed on frequently queried columns  
✅ **Sequences** - Auto-incrementing primary keys  
✅ **Date Tracking** - Creation and update timestamps  
✅ **Notes Support** - Optional notes for audit documentation

## Performance Considerations

- **Indexes** are created on BRANCH_ID, SYSTEM_ID, and GRANTED_BY for fast lookups
- **Unique Constraints** prevent duplicate permissions: `UK_BRANCH_SYSTEM` and `UK_BRANCH_SCREEN`
- **Efficient Joins** with SYS_SYSTEM and SYS_SCREEN tables for enriched data
- **Soft Deletes** maintain audit trail while allowing logical revocation

## Related Components

- **BranchPermissionsController** (C# API)
  - Exposes REST endpoints for permission management
  - Uses these procedures via OracleDataReader

- **BranchPermissionRepository** (C# Data Access)
  - Implements IBranchPermissionRepository interface
  - Calls stored procedures through OracleConnection

- **DTOs** (C# Models)
  - BranchSystemDto
  - BranchScreenDto
  - GrantSystemAccessDto
  - GrantScreenAccessDto

## Troubleshooting

**Problem:** "SEQ_SYS_BRANCH_SYSTEM does not exist"  
**Solution:** Execute 88_Create_Branch_Level_Permissions.sql first to create sequences

**Problem:** "FK_BRANCH_SYSTEM_BRANCH constraint violated"  
**Solution:** Ensure branch exists in SYS_BRANCH table before granting permissions

**Problem:** "Duplicate key value" on unique constraint  
**Solution:** Use GRANT procedures - they handle duplicates by updating existing records

## Future Enhancements

- Batch grant/revoke operations
- Scheduled permission expiration
- Role-based permission inheritance
- Permission conflict resolution
- Real-time permission cache invalidation

---
Generated: 2025
Part of: ThinkOnErp System
Database: Oracle 19c+
