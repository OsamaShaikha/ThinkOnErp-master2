-- =====================================================
-- Migration Script Execution Index
-- Branch Permissions System Scripts
-- =====================================================
-- Last Updated: 2025
-- Sequence: 89 → 90 → 91
-- =====================================================

/*
EXECUTION SEQUENCE (in order):

1. Database/Scripts/88_Create_Branch_Level_Permissions.sql
   └─ Status: EXISTING (already in database)
   └─ Creates: SYS_BRANCH_SYSTEM table
   └─ Creates: SYS_BRANCH_SCREEN table
   └─ Creates: Sequences & Indexes

2. Database/Scripts/89_Create_Branch_Permissions_Procedures.sql
   ├─ NEW SCRIPT (created for this implementation)
   ├─ Depends on: Script 88
   ├─ Creates: 12 Stored Procedures
   │  ├─ SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH
   │  ├─ SP_BRANCH_SCREENS_SELECT_BY_BRANCH
   │  ├─ SP_GET_ALL_SYSTEMS
   │  ├─ SP_GET_ALL_SCREENS
   │  ├─ SP_GET_SCREENS_BY_SYSTEM
   │  ├─ SP_BRANCH_SYSTEM_GRANT
   │  ├─ SP_BRANCH_SYSTEM_REVOKE
   │  ├─ SP_BRANCH_SCREEN_GRANT
   │  ├─ SP_BRANCH_SCREEN_REVOKE
   │  ├─ SP_CHECK_BRANCH_SYSTEM_ALLOWED
   │  └─ SP_CHECK_BRANCH_SCREEN_ALLOWED
   └─ Time: ~5 seconds

3. Database/Scripts/90_Sample_Branch_Permissions_Data.sql
   ├─ NEW SCRIPT (created for testing)
   ├─ Depends on: Script 88 & 89
   ├─ Inserts: 7 Sample records
   ├─ Status: OPTIONAL (for testing/demo only)
   └─ Time: ~2 seconds

4. Database/Scripts/91_Branch_Permissions_Query_Utilities.sql
   ├─ NEW SCRIPT (created for reporting)
   ├─ Depends on: Script 88
   ├─ Provides: 10 Utility queries
   ├─ Status: OPTIONAL (run as needed for analysis)
   └─ Time: Immediate (read-only queries)

TOTAL SCRIPTS TO EXECUTE: 3
NEW SCRIPTS: 3
EXISTING SCRIPTS: 1 (Script 88)

*/

-- =====================================================
-- FILES CREATED
-- =====================================================

/*
✅ Database/Scripts/89_Create_Branch_Permissions_Procedures.sql
   • Location: /Database/Scripts/
   • Size: ~15 KB
   • Type: Oracle Stored Procedures
   • Dependency: SYS_BRANCH_SYSTEM, SYS_BRANCH_SCREEN tables
   • Execution: ONCE (idempotent with CREATE OR REPLACE)

✅ Database/Scripts/90_Sample_Branch_Permissions_Data.sql
   • Location: /Database/Scripts/
   • Size: ~3 KB
   • Type: Data Insert Script
   • Dependency: Procedures 89 + Tables 88
   • Execution: Optional (for testing)
   • WARNING: Will insert sample data - adjust as needed

✅ Database/Scripts/91_Branch_Permissions_Query_Utilities.sql
   • Location: /Database/Scripts/
   • Size: ~12 KB
   • Type: Query Templates
   • Dependency: SYS_BRANCH_SYSTEM, SYS_BRANCH_SCREEN tables
   • Execution: As needed (read-only)

✅ Database/Scripts/README_BRANCH_PERMISSIONS.md
   • Location: /Database/Scripts/
   • Type: Documentation
   • Provides: Complete usage guide
*/

-- =====================================================
-- DEPLOYMENT CHECKLIST
-- =====================================================

/*
STEP 1: Backup Current Database
  ☐ Backup all production data
  ☐ Test backup restore procedure

STEP 2: Execute Script 89 - Create Procedures
  ☐ Execute: 89_Create_Branch_Permissions_Procedures.sql
  ☐ Verify: 12 procedures created
  ☐ Test: Run a sample procedure

STEP 3: Execute Script 90 - Sample Data (OPTIONAL)
  ☐ Execute: 90_Sample_Branch_Permissions_Data.sql
  ☐ Verify: 7 sample records inserted
  ☐ Delete if production environment

STEP 4: Run Script 91 - Validation Queries (OPTIONAL)
  ☐ Execute: 91_Branch_Permissions_Query_Utilities.sql
  ☐ Review: Results from query #9 (integrity check)
  ☐ Confirm: No orphaned records

STEP 5: Test API Integration
  ☐ Build: ThinkOnErp.API project
  ☐ Start: API server
  ☐ Test: /api/branchpermissions endpoints
  ☐ Verify: Data integrity

STEP 6: Production Deployment
  ☐ Run scripts on production database
  ☐ Update: Documentation/wiki
  ☐ Notify: Dev team
  ☐ Monitor: Database performance
*/

-- =====================================================
-- RELATED CODE CHANGES
-- =====================================================

/*
✅ BACKEND:
  • src/ThinkOnErp.API/Controllers/BranchPermissionsController.cs (NEW)
	- 7 GET/POST endpoints for branch permissions
	- Full authorization and error handling
	- Swagger documentation included

  • src/ThinkOnErp.Infrastructure/Repositories/BranchPermissionRepository.cs (EXISTING)
	- Already calls stored procedures
	- Mapper methods fixed for correct property names
	- Now registered in DependencyInjection.cs

  • src/ThinkOnErp.Infrastructure/DependencyInjection.cs
	- Added: services.AddScoped<IBranchPermissionRepository, BranchPermissionRepository>();

✅ FEATURES ENABLED:
  • Get all systems for a branch
  • Get all screens for a branch
  • Grant system access to branch
  • Revoke system access from branch
  • Grant screen access to branch
  • Revoke screen access from branch
  • Get all available systems
*/

-- =====================================================
-- PROCEDURE SUMMARY
-- =====================================================

/*
QUERY PROCEDURES (Read-Only):
1. SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH(P_BRANCH_ID) → List[BranchSystemDto]
2. SP_BRANCH_SCREENS_SELECT_BY_BRANCH(P_BRANCH_ID) → List[BranchScreenDto]
3. SP_GET_ALL_SYSTEMS() → List[SystemDto]
4. SP_GET_ALL_SCREENS() → List[ScreenDto]
5. SP_GET_SCREENS_BY_SYSTEM(P_SYSTEM_ID) → List[ScreenDto]
6. SP_CHECK_BRANCH_SYSTEM_ALLOWED(P_BRANCH_ID, P_SYSTEM_ID) → CHAR
7. SP_CHECK_BRANCH_SCREEN_ALLOWED(P_BRANCH_ID, P_SCREEN_ID) → CHAR

WRITE PROCEDURES (Modify Data):
8. SP_BRANCH_SYSTEM_GRANT(P_BRANCH_ID, P_SYSTEM_ID, ...) → NEW_ID
9. SP_BRANCH_SYSTEM_REVOKE(P_BRANCH_ID, P_SYSTEM_ID, ...) → UPDATED_ID
10. SP_BRANCH_SCREEN_GRANT(P_BRANCH_ID, P_SCREEN_ID, ...) → NEW_ID
11. SP_BRANCH_SCREEN_REVOKE(P_BRANCH_ID, P_SCREEN_ID, ...) → UPDATED_ID
*/

-- =====================================================
-- ROLLBACK PROCEDURE (if needed)
-- =====================================================

/*
To rollback these changes:

1. DROP procedures:
   DROP PROCEDURE SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH;
   DROP PROCEDURE SP_BRANCH_SCREENS_SELECT_BY_BRANCH;
   DROP PROCEDURE SP_GET_ALL_SYSTEMS;
   DROP PROCEDURE SP_GET_ALL_SCREENS;
   DROP PROCEDURE SP_GET_SCREENS_BY_SYSTEM;
   DROP PROCEDURE SP_BRANCH_SYSTEM_GRANT;
   DROP PROCEDURE SP_BRANCH_SYSTEM_REVOKE;
   DROP PROCEDURE SP_BRANCH_SCREEN_GRANT;
   DROP PROCEDURE SP_BRANCH_SCREEN_REVOKE;
   DROP PROCEDURE SP_CHECK_BRANCH_SYSTEM_ALLOWED;
   DROP PROCEDURE SP_CHECK_BRANCH_SCREEN_ALLOWED;

2. Tables SYS_BRANCH_SYSTEM and SYS_BRANCH_SCREEN remain (created by script 88)

3. Remove sample data (if inserted):
   DELETE FROM SYS_BRANCH_SYSTEM WHERE CREATION_USER = 'admin';
   DELETE FROM SYS_BRANCH_SCREEN WHERE CREATION_USER = 'admin';
   COMMIT;

4. Remove API controller: Delete BranchPermissionsController.cs

5. Remove DI registration from DependencyInjection.cs
*/

-- =====================================================
-- MONITORING & MAINTENANCE
-- =====================================================

/*
Monitor Execution:
  • Run query #10 from 91_Branch_Permissions_Query_Utilities.sql
  • Check permission statistics by branch
  • Identify under-utilized branches

Validate Integrity:
  • Run query #9 from 91_Branch_Permissions_Query_Utilities.sql
  • Check for orphaned records monthly
  • Ensure all foreign keys are valid

Performance Tuning:
  • Monitor index usage
  • Check execution plans
  • Analyze table statistics

Audit Trail:
  • Review recent changes (query #8)
  • Track permission modifications
  • Maintain compliance documentation
*/

-- =====================================================
-- Version Information
-- =====================================================

/*
Database Scripts Version: 1.0
Created: 2025
Author: AI Assistant
Target Database: Oracle 19c+
Target Framework: .NET 8
API Controller: BranchPermissionsController.cs

Scripts in this batch:
  • 89_Create_Branch_Permissions_Procedures.sql (NEW)
  • 90_Sample_Branch_Permissions_Data.sql (NEW)
  • 91_Branch_Permissions_Query_Utilities.sql (NEW)
  • README_BRANCH_PERMISSIONS.md (NEW)
*/
