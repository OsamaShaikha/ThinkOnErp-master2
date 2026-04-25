-- =============================================
-- Script: 43_Remove_FiscalYearId_From_Company.sql
-- Description: Remove FISCAL_YEAR_ID column from SYS_COMPANY table
-- Rationale: Fiscal years are associated with branches, not companies directly
-- Author: System
-- Date: 2026-04-26
-- =============================================

PROMPT ========================================
PROMPT Removing FISCAL_YEAR_ID from SYS_COMPANY Table
PROMPT ========================================

-- =============================================
-- Step 1: Drop foreign key constraint
-- =============================================
PROMPT 'Dropping foreign key constraint FK_COMPANY_FISCAL_YEAR...';

BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_COMPANY DROP CONSTRAINT FK_COMPANY_FISCAL_YEAR';
    DBMS_OUTPUT.PUT_LINE('Foreign key constraint dropped successfully.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_COMPANY_FISCAL_YEAR does not exist. Skipping...');
        ELSE
            RAISE;
        END IF;
END;
/

-- =============================================
-- Step 2: Drop index if exists
-- =============================================
PROMPT 'Dropping index IDX_COMPANY_FISCAL_YEAR...';

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_COMPANY_FISCAL_YEAR';
    DBMS_OUTPUT.PUT_LINE('Index dropped successfully.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_COMPANY_FISCAL_YEAR does not exist. Skipping...');
        ELSE
            RAISE;
        END IF;
END;
/

-- =============================================
-- Step 3: Drop the FISCAL_YEAR_ID column
-- =============================================
PROMPT 'Dropping FISCAL_YEAR_ID column from SYS_COMPANY table...';

BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_COMPANY DROP COLUMN FISCAL_YEAR_ID';
    DBMS_OUTPUT.PUT_LINE('FISCAL_YEAR_ID column dropped successfully.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -904 THEN
            DBMS_OUTPUT.PUT_LINE('Column FISCAL_YEAR_ID does not exist. Skipping...');
        ELSE
            RAISE;
        END IF;
END;
/

-- =============================================
-- Step 4: Verification
-- =============================================
PROMPT 'Verifying column removal...';

-- Check if FISCAL_YEAR_ID column still exists
SELECT 
    CASE 
        WHEN COUNT(*) = 0 THEN 'SUCCESS: FISCAL_YEAR_ID column removed from SYS_COMPANY'
        ELSE 'ERROR: FISCAL_YEAR_ID column still exists in SYS_COMPANY'
    END AS VERIFICATION_RESULT
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
  AND column_name = 'FISCAL_YEAR_ID';

-- Display current SYS_COMPANY table structure
PROMPT '';
PROMPT 'Current SYS_COMPANY table structure:';
SELECT 
    column_name,
    data_type,
    data_length,
    nullable,
    data_default
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
ORDER BY column_id;

-- Display current constraints on SYS_COMPANY
PROMPT '';
PROMPT 'Current constraints on SYS_COMPANY:';
SELECT 
    constraint_name,
    constraint_type,
    CASE constraint_type
        WHEN 'P' THEN 'Primary Key'
        WHEN 'R' THEN 'Foreign Key'
        WHEN 'U' THEN 'Unique'
        WHEN 'C' THEN 'Check'
        ELSE constraint_type
    END AS constraint_type_desc
FROM user_constraints
WHERE table_name = 'SYS_COMPANY'
ORDER BY constraint_type, constraint_name;

PROMPT ========================================
PROMPT Script Completed Successfully
PROMPT ========================================
PROMPT 
PROMPT Summary:
PROMPT - Dropped FK_COMPANY_FISCAL_YEAR foreign key constraint
PROMPT - Dropped IDX_COMPANY_FISCAL_YEAR index
PROMPT - Removed FISCAL_YEAR_ID column from SYS_COMPANY table
PROMPT 
PROMPT Rationale:
PROMPT - Fiscal years are now associated with both COMPANY_ID and BRANCH_ID
PROMPT - Companies do not directly reference fiscal years
PROMPT - Fiscal years are managed at the branch level
PROMPT - Default fiscal year is automatically created when creating a company
PROMPT 
