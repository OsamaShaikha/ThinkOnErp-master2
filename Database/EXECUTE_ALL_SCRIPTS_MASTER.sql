-- =====================================================
-- MASTER SCRIPT - Execute ALL Database Scripts
-- Database: THINKON_ERP/THINKON_ERP
-- Server: 178.104.126.99:1521/XEPDB1
-- =====================================================
-- This script executes ALL SQL scripts in the correct order
-- to build the complete ThinkOnERP database from scratch
-- =====================================================

-- Set SQL*Plus environment
SET SERVEROUTPUT ON SIZE UNLIMITED
SET ECHO ON
SET FEEDBACK ON
SET VERIFY OFF
SET LINESIZE 200
SET PAGESIZE 1000
SET TIMING ON

-- Create spool file for logging
SPOOL master_execution.log

PROMPT =====================================================
PROMPT ThinkOnERP - Master Database Setup Script
PROMPT =====================================================
PROMPT Database: THINKON_ERP
PROMPT Server: 178.104.126.99:1521/XEPDB1
PROMPT Start Time: 
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS start_time FROM DUAL;
PROMPT =====================================================
PROMPT

-- =====================================================
-- PHASE 1: CORE TABLES AND SEQUENCES
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 1: Core Tables and Sequences
PROMPT =====================================================
PROMPT

PROMPT [01] Creating Sequences...
@@Scripts/01_Create_Sequences.sql

PROMPT [02] Creating SYS_ROLE Procedures...
@@Scripts/02_Create_SYS_ROLE_Procedures.sql

PROMPT [03] Creating SYS_CURRENCY Procedures...
@@Scripts/03_Create_SYS_CURRENCY_Procedures.sql

PROMPT [04] Creating SYS_BRANCH Procedures...
@@Scripts/04_Create_SYS_BRANCH_Procedures.sql

PROMPT [04] Creating SYS_COMPANY Procedures...
@@Scripts/04_Create_SYS_COMPANY_Procedures.sql

PROMPT [05] Creating SYS_USERS Procedures...
@@Scripts/05_Create_SYS_USERS_Procedures.sql

PROMPT [06] Inserting Test Data...
@@Scripts/06_Insert_Test_Data.sql

-- =====================================================
-- PHASE 2: AUTHENTICATION AND SECURITY
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 2: Authentication and Security
PROMPT =====================================================
PROMPT

PROMPT [07] Adding RefreshToken to Users...
@@Scripts/07_Add_RefreshToken_To_Users.sql

PROMPT [08] Creating Permissions Tables...
@@Scripts/08_Create_Permissions_Tables.sql

PROMPT [09] Creating Permissions Sequences...
@@Scripts/09_Create_Permissions_Sequences.sql

PROMPT [10] Creating Permissions Procedures...
@@Scripts/10_Create_Permissions_Procedures.sql

PROMPT [10] Creating SYS_SUPER_ADMIN Procedures...
@@Scripts/10_Create_SYS_SUPER_ADMIN_Procedures.sql

PROMPT [11] Inserting Permissions Seed Data...
@@Scripts/11_Insert_Permissions_Seed_Data.sql

PROMPT [12] Adding Force Logout Column...
@@Scripts/12_Add_Force_Logout_Column.sql

-- =====================================================
-- PHASE 3: AUDIT AND TRACEABILITY SYSTEM
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 3: Audit and Traceability System
PROMPT =====================================================
PROMPT

PROMPT [13] Extending SYS_AUDIT_LOG for Traceability...
@@Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql

PROMPT [14] Creating Audit Archive Table...
@@Scripts/14_Create_Audit_Archive_Table.sql

PROMPT [15] Creating Performance Metrics Tables...
@@Scripts/15_Create_Performance_Metrics_Tables.sql

PROMPT [16] Creating Security Monitoring Tables...
@@Scripts/16_Create_Security_Monitoring_Tables.sql

PROMPT [17] Creating Retention Policy Table...
@@Scripts/17_Create_Retention_Policy_Table.sql

PROMPT [18] Adding Audit Table Comments...
@@Scripts/18_Add_Audit_Table_Comments.sql

-- =====================================================
-- PHASE 4: FISCAL YEAR AND COMPANY EXTENSIONS
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 4: Fiscal Year and Company Extensions
PROMPT =====================================================
PROMPT

PROMPT [18] Creating SYS_FISCAL_YEAR Table...
@@Scripts/18_Create_SYS_FISCAL_YEAR_Table.sql

PROMPT [19] Extending SYS_COMPANY Table...
@@Scripts/19_Extend_SYS_COMPANY_Table.sql

PROMPT [20] Updating SYS_COMPANY Procedures...
@@Scripts/20_Update_SYS_COMPANY_Procedures.sql

PROMPT [21] Inserting Fiscal Year Test Data...
@@Scripts/21_Insert_Fiscal_Year_Test_Data.sql

PROMPT [22] Updating Company Test Data...
@@Scripts/22_Update_Company_Test_Data.sql

PROMPT [23] Creating Company With Default Branch...
@@Scripts/23_Create_Company_With_Default_Branch.sql

PROMPT [24] Adding Branch Logo Support...
@@Scripts/24_Add_Branch_Logo_Support.sql

PROMPT [25] Adding Default Branch to Company...
@@Scripts/25_Add_Default_Branch_To_Company.sql

-- =====================================================
-- PHASE 5: SUPER ADMIN AND PASSWORD MANAGEMENT
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 5: Super Admin and Password Management
PROMPT =====================================================
PROMPT

PROMPT [26] Adding SuperAdmin Login Procedure...
@@Scripts/26_Add_SuperAdmin_Login_Procedure.sql

PROMPT [27] Inserting SuperAdmin Seed Data...
@@Scripts/27_Insert_SuperAdmin_Seed_Data.sql

PROMPT [30] Adding User Change Password Procedure...
@@Scripts/30_Add_User_Change_Password_Procedure.sql

PROMPT [31] Hashing Existing Plain Text Passwords...
@@Scripts/31_Hash_Existing_Plain_Text_Passwords.sql

-- =====================================================
-- PHASE 6: SCHEMA REFINEMENTS
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 6: Schema Refinements
PROMPT =====================================================
PROMPT

PROMPT [32] Moving Fields From Company To Branch...
@@Scripts/32_Move_Fields_From_Company_To_Branch.sql

PROMPT [33] Removing SystemLanguage Column...
@@Scripts/33_Remove_SystemLanguage_Column.sql

PROMPT [34] Recreating Company Procedures Final...
@@Scripts/34_Recreate_Company_Procedures_Final.sql

-- =====================================================
-- PHASE 7: TICKET MANAGEMENT SYSTEM
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 7: Ticket Management System
PROMPT =====================================================
PROMPT

PROMPT [35] Creating Ticket Tables...
@@Scripts/35_Create_Ticket_Tables.sql

PROMPT [36] Creating Ticket Procedures...
@@Scripts/36_Create_Ticket_Procedures.sql

PROMPT [37] Creating Ticket Support Procedures...
@@Scripts/37_Create_Ticket_Support_Procedures.sql

PROMPT [39] Creating Additional Ticket Support Procedures...
@@Scripts/39_Create_Additional_Ticket_Support_Procedures.sql

-- =====================================================
-- PHASE 8: FISCAL YEAR ENHANCEMENTS
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 8: Fiscal Year Enhancements
PROMPT =====================================================
PROMPT

PROMPT [40] Adding BranchId to FiscalYear...
@@Scripts/40_Add_BranchId_To_FiscalYear.sql

PROMPT [41] Updating FiscalYear Procedures for BranchId...
@@Scripts/41_Update_FiscalYear_Procedures_For_BranchId.sql

PROMPT [42] Updating Company Procedure with Default FiscalYear...
@@Scripts/42_Update_Company_Procedure_With_Default_FiscalYear.sql

PROMPT [43] Removing FiscalYearId from Company...
@@Scripts/43_Remove_FiscalYearId_From_Company.sql

PROMPT [45] Fixing Company Procedure Complete...
@@Scripts/45_Fix_Company_Procedure_Complete.sql

PROMPT [46] Fixing Company Select Procedures...
@@Scripts/46_Fix_Company_Select_Procedures.sql

-- =====================================================
-- PHASE 9: ADVANCED SEARCH AND CONFIGURATION
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 9: Advanced Search and Configuration
PROMPT =====================================================
PROMPT

PROMPT [47] Creating Saved Search Tables...
@@Scripts/47_Create_Saved_Search_Tables.sql

PROMPT [47] Creating Ticket Configuration Table...
@@Scripts/47_Create_Ticket_Configuration_Table.sql

PROMPT [48] Creating Advanced Search Procedure...
@@Scripts/48_Create_Advanced_Search_Procedure.sql

PROMPT [48] Creating Ticket Configuration Procedures...
@@Scripts/48_Create_Ticket_Configuration_Procedures.sql

PROMPT [49] Creating Search Analytics Table...
@@Scripts/49_Create_Search_Analytics_Table.sql

-- =====================================================
-- PHASE 10: AUDIT TRAIL PROCEDURES
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 10: Audit Trail Procedures
PROMPT =====================================================
PROMPT

PROMPT [54] Creating Audit Trail Procedures...
@@Scripts/54_Create_Audit_Trail_Procedures.sql

PROMPT [55] Fixing Company Procedures Match Schema...
@@Scripts/55_Fix_Company_Procedures_Match_Schema.sql

PROMPT [56] Fixing SYS_AUDIT_LOG Column Types...
@@Scripts/56_Fix_SYS_AUDIT_LOG_Column_Types.sql

-- =====================================================
-- PHASE 11: LEGACY COMPATIBILITY AND ENHANCEMENTS
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 11: Legacy Compatibility and Enhancements
PROMPT =====================================================
PROMPT

PROMPT [57] Adding Legacy Compatibility Columns...
@@Scripts/57_Add_Legacy_Compatibility_Columns.sql

PROMPT [57] Creating Legacy Audit Procedures...
@@Scripts/57_Create_Legacy_Audit_Procedures.sql

PROMPT [57] Updating SYS_FAILED_LOGINS Add UserAgent...
@@Scripts/57_Update_SYS_FAILED_LOGINS_Add_UserAgent.sql

PROMPT [57] Adding Foreign Key Constraint BRANCH_ID...
@@Scripts/57_Add_Foreign_Key_Constraint_BRANCH_ID.sql

-- =====================================================
-- PHASE 12: AUDIT STATUS TRACKING
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 12: Audit Status Tracking
PROMPT =====================================================
PROMPT

PROMPT [58] Creating SYS_AUDIT_STATUS_TRACKING Table...
@@Scripts/58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql

PROMPT [58] Updating SYS_AUDIT_LOG_ARCHIVE Add Legacy Columns...
@@Scripts/58_Update_SYS_AUDIT_LOG_ARCHIVE_Add_Legacy_Columns.sql

PROMPT [59] Validating Archive Table Structure...
@@Scripts/59_Validate_Archive_Table_Structure.sql

-- =====================================================
-- PHASE 13: PERFORMANCE OPTIMIZATION
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 13: Performance Optimization
PROMPT =====================================================
PROMPT

PROMPT [59] Creating Performance Indexes Task 1.5...
@@Scripts/59_Create_Performance_Indexes_Task_1_5.sql

PROMPT [60] Creating Composite Indexes Task 1.6...
@@Scripts/60_Create_Composite_Indexes_Task_1_6.sql

PROMPT [60] Verifying Composite Indexes...
@@Scripts/60_Verify_Composite_Indexes.sql

PROMPT [61] Adding Security Threats Foreign Keys...
@@Scripts/61_Add_Security_Threats_Foreign_Keys.sql

-- =====================================================
-- PHASE 14: ADVANCED SEARCH AND REPORTING
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 14: Advanced Search and Reporting
PROMPT =====================================================
PROMPT

PROMPT [73] Updating Legacy Audit Search...
@@Scripts/73_Update_Legacy_Audit_Search.sql

PROMPT [74] Adding Search Performance Indexes...
@@Scripts/74_Add_Search_Performance_Indexes.sql

PROMPT [76] Creating Report Schedule Table...
@@Scripts/76_Create_Report_Schedule_Table.sql

PROMPT [78] Creating Covering Indexes for Audit Queries...
@@Scripts/78_Create_Covering_Indexes_For_Audit_Queries.sql

-- =====================================================
-- PHASE 15: AUDIT LOG PARTITIONING (OPTIONAL)
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 15: Audit Log Partitioning (Optional)
PROMPT =====================================================
PROMPT Note: Partitioning scripts are commented out by default
PROMPT Uncomment if you need partitioning support
PROMPT

-- PROMPT [78] Implementing Audit Log Partitioning...
-- @@Scripts/78_Implement_Audit_Log_Partitioning.sql

-- PROMPT [79] Migrating Existing Audit Log Data...
-- @@Scripts/79_Migrate_Existing_Audit_Log_Data.sql

-- PROMPT [80] Populating Branch ID From Context...
-- @@Scripts/80_Populate_Branch_ID_From_Context.sql

-- PROMPT [81] Validating Audit Log Migration...
-- @@Scripts/81_Validate_Audit_Log_Migration.sql

-- =====================================================
-- PHASE 16: INDEX MAINTENANCE
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT PHASE 16: Index Maintenance
PROMPT =====================================================
PROMPT

PROMPT [84] Creating Indexes With Online Rebuild...
@@Scripts/84_Create_Indexes_With_Online_Rebuild.sql

-- =====================================================
-- COMPLETION AND VERIFICATION
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT Database Setup Completed!
PROMPT =====================================================
PROMPT

PROMPT Verifying Database Objects...
PROMPT

-- Count all tables
SELECT 'Total Tables: ' || COUNT(*) AS status FROM USER_TABLES;

-- Count all sequences
SELECT 'Total Sequences: ' || COUNT(*) AS status FROM USER_SEQUENCES;

-- Count all procedures
SELECT 'Total Procedures: ' || COUNT(*) AS status 
FROM USER_OBJECTS WHERE OBJECT_TYPE = 'PROCEDURE';

-- Count all indexes
SELECT 'Total Indexes: ' || COUNT(*) AS status FROM USER_INDEXES;

-- List key tables
PROMPT
PROMPT Key Tables Created:
SELECT TABLE_NAME, NUM_ROWS, STATUS 
FROM USER_TABLES 
WHERE TABLE_NAME IN (
    'SYS_ROLE',
    'SYS_CURRENCY',
    'SYS_COMPANY',
    'SYS_BRANCH',
    'SYS_USERS',
    'SYS_SUPER_ADMIN',
    'SYS_AUDIT_LOG',
    'SYS_AUDIT_LOG_ARCHIVE',
    'SYS_AUDIT_STATUS_TRACKING',
    'SYS_PERFORMANCE_METRICS',
    'SYS_SECURITY_THREATS',
    'SYS_FISCAL_YEAR',
    'SYS_TICKET',
    'SYS_TICKET_COMMENT',
    'SYS_TICKET_ATTACHMENT',
    'SYS_SAVED_SEARCH',
    'SYS_PERMISSIONS',
    'SYS_ROLE_PERMISSIONS'
)
ORDER BY TABLE_NAME;

-- Check for invalid objects
PROMPT
PROMPT Checking for Invalid Objects:
SELECT OBJECT_TYPE, OBJECT_NAME, STATUS 
FROM USER_OBJECTS 
WHERE STATUS = 'INVALID'
ORDER BY OBJECT_TYPE, OBJECT_NAME;

-- Display completion time
PROMPT
PROMPT End Time:
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS end_time FROM DUAL;

PROMPT
PROMPT =====================================================
PROMPT Execution log saved to: master_execution.log
PROMPT =====================================================
PROMPT

SPOOL OFF

-- Display summary message
PROMPT
PROMPT =====================================================
PROMPT ThinkOnERP Database Setup Complete!
PROMPT =====================================================
PROMPT
PROMPT Next Steps:
PROMPT 1. Review master_execution.log for any errors
PROMPT 2. Check for invalid objects (shown above)
PROMPT 3. Verify test data was inserted correctly
PROMPT 4. Update your application connection string
PROMPT 5. Run the application and test functionality
PROMPT
PROMPT =====================================================
