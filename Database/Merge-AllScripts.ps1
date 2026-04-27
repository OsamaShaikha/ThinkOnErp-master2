#!/usr/bin/env pwsh
# =====================================================
# Merge All SQL Scripts into One File
# Creates: ALL_SCRIPTS_CONSOLIDATED.sql
# =====================================================

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Merging All SQL Scripts into One File" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Change to Database directory
Set-Location $PSScriptRoot

# Output file
$outputFile = "ALL_SCRIPTS_CONSOLIDATED.sql"

# Remove existing file if it exists
if (Test-Path $outputFile) {
    Remove-Item $outputFile
    Write-Host "Removed existing file: $outputFile" -ForegroundColor Yellow
}

# Create header
$header = @"
-- =====================================================
-- ThinkOnERP - ALL SCRIPTS CONSOLIDATED
-- Database: THINKON_ERP/THINKON_ERP
-- Server: 178.104.126.99:1521/XEPDB1
-- =====================================================
-- This file contains ALL SQL scripts merged into one file
-- Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
-- =====================================================

SET SERVEROUTPUT ON SIZE UNLIMITED
SET ECHO ON
SET FEEDBACK ON
SET VERIFY OFF
SET LINESIZE 200
SET PAGESIZE 1000
SET TIMING ON

SPOOL consolidated_execution.log

PROMPT =====================================================
PROMPT ThinkOnERP - Consolidated Script Execution
PROMPT =====================================================
PROMPT Start Time: 
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS start_time FROM DUAL;
PROMPT =====================================================
PROMPT

"@

Add-Content -Path $outputFile -Value $header

# Define script order
$scriptOrder = @(
    "01_Create_Sequences.sql",
    "02_Create_SYS_ROLE_Procedures.sql",
    "03_Create_SYS_CURRENCY_Procedures.sql",
    "04_Create_SYS_BRANCH_Procedures.sql",
    "04_Create_SYS_COMPANY_Procedures.sql",
    "05_Create_SYS_USERS_Procedures.sql",
    "06_Insert_Test_Data.sql",
    "07_Add_RefreshToken_To_Users.sql",
    "08_Create_Permissions_Tables.sql",
    "09_Create_Permissions_Sequences.sql",
    "10_Create_Permissions_Procedures.sql",
    "10_Create_SYS_SUPER_ADMIN_Procedures.sql",
    "11_Insert_Permissions_Seed_Data.sql",
    "12_Add_Force_Logout_Column.sql",
    "13_Extend_SYS_AUDIT_LOG_For_Traceability.sql",
    "14_Create_Audit_Archive_Table.sql",
    "15_Create_Performance_Metrics_Tables.sql",
    "16_Create_Security_Monitoring_Tables.sql",
    "17_Create_Retention_Policy_Table.sql",
    "18_Add_Audit_Table_Comments.sql",
    "18_Create_SYS_FISCAL_YEAR_Table.sql",
    "19_Extend_SYS_COMPANY_Table.sql",
    "20_Update_SYS_COMPANY_Procedures.sql",
    "21_Insert_Fiscal_Year_Test_Data.sql",
    "22_Update_Company_Test_Data.sql",
    "23_Create_Company_With_Default_Branch.sql",
    "24_Add_Branch_Logo_Support.sql",
    "25_Add_Default_Branch_To_Company.sql",
    "26_Add_SuperAdmin_Login_Procedure.sql",
    "27_Insert_SuperAdmin_Seed_Data.sql",
    "30_Add_User_Change_Password_Procedure.sql",
    "31_Hash_Existing_Plain_Text_Passwords.sql",
    "32_Move_Fields_From_Company_To_Branch.sql",
    "33_Remove_SystemLanguage_Column.sql",
    "34_Recreate_Company_Procedures_Final.sql",
    "35_Create_Ticket_Tables.sql",
    "36_Create_Ticket_Procedures.sql",
    "37_Create_Ticket_Support_Procedures.sql",
    "39_Create_Additional_Ticket_Support_Procedures.sql",
    "40_Add_BranchId_To_FiscalYear.sql",
    "41_Update_FiscalYear_Procedures_For_BranchId.sql",
    "42_Update_Company_Procedure_With_Default_FiscalYear.sql",
    "43_Remove_FiscalYearId_From_Company.sql",
    "45_Fix_Company_Procedure_Complete.sql",
    "46_Fix_Company_Select_Procedures.sql",
    "47_Create_Saved_Search_Tables.sql",
    "47_Create_Ticket_Configuration_Table.sql",
    "48_Create_Advanced_Search_Procedure.sql",
    "48_Create_Ticket_Configuration_Procedures.sql",
    "49_Create_Search_Analytics_Table.sql",
    "54_Create_Audit_Trail_Procedures.sql",
    "55_Fix_Company_Procedures_Match_Schema.sql",
    "56_Fix_SYS_AUDIT_LOG_Column_Types.sql",
    "57_Add_Legacy_Compatibility_Columns.sql",
    "57_Create_Legacy_Audit_Procedures.sql",
    "57_Update_SYS_FAILED_LOGINS_Add_UserAgent.sql",
    "57_Add_Foreign_Key_Constraint_BRANCH_ID.sql",
    "58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql",
    "58_Update_SYS_AUDIT_LOG_ARCHIVE_Add_Legacy_Columns.sql",
    "59_Validate_Archive_Table_Structure.sql",
    "59_Create_Performance_Indexes_Task_1_5.sql",
    "60_Create_Composite_Indexes_Task_1_6.sql",
    "60_Verify_Composite_Indexes.sql",
    "61_Add_Security_Threats_Foreign_Keys.sql",
    "73_Update_Legacy_Audit_Search.sql",
    "74_Add_Search_Performance_Indexes.sql",
    "76_Create_Report_Schedule_Table.sql",
    "78_Create_Covering_Indexes_For_Audit_Queries.sql",
    "84_Create_Indexes_With_Online_Rebuild.sql"
)

$scriptCount = 0
$totalScripts = $scriptOrder.Count

foreach ($scriptName in $scriptOrder) {
    $scriptPath = "Scripts\$scriptName"
    
    if (Test-Path $scriptPath) {
        $scriptCount++
        Write-Host "[$scriptCount/$totalScripts] Merging: $scriptName" -ForegroundColor Green
        
        # Add separator
        $separator = @"

-- =====================================================
-- SCRIPT: $scriptName
-- =====================================================

"@
        Add-Content -Path $outputFile -Value $separator
        
        # Add script content
        $content = Get-Content -Path $scriptPath -Raw
        Add-Content -Path $outputFile -Value $content
        
        # Add commit after each script
        Add-Content -Path $outputFile -Value "`nCOMMIT;`n"
    }
    else {
        Write-Host "[$scriptCount/$totalScripts] SKIPPED (not found): $scriptName" -ForegroundColor Yellow
    }
}

# Add footer
$footer = @"

-- =====================================================
-- COMPLETION AND VERIFICATION
-- =====================================================

PROMPT
PROMPT =====================================================
PROMPT Database Setup Completed!
PROMPT =====================================================
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
    'SYS_SAVED_SEARCH',
    'SYS_PERMISSIONS'
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
PROMPT Execution log saved to: consolidated_execution.log
PROMPT =====================================================

SPOOL OFF

PROMPT
PROMPT =====================================================
PROMPT ThinkOnERP Database Setup Complete!
PROMPT =====================================================
PROMPT
PROMPT $scriptCount scripts executed successfully
PROMPT Check consolidated_execution.log for details
PROMPT
PROMPT =====================================================
"@

Add-Content -Path $outputFile -Value $footer

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Merge Complete!" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output File: $outputFile" -ForegroundColor Yellow
Write-Host "Scripts Merged: $scriptCount" -ForegroundColor Yellow
Write-Host "File Size: $((Get-Item $outputFile).Length / 1KB) KB" -ForegroundColor Yellow
Write-Host ""
Write-Host "You can now execute this file with:" -ForegroundColor White
Write-Host "  sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @ALL_SCRIPTS_CONSOLIDATED.sql" -ForegroundColor Cyan
Write-Host ""
