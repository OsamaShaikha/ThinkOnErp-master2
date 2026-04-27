-- =============================================
-- Script: 55_Fix_Company_Procedures_Match_Schema.sql
-- Description: Fix SYS_COMPANY stored procedures to match actual table schema
-- This removes references to non-existent columns and ensures procedures match the real table
-- Author: System
-- Date: 2026-04-27
-- =============================================

PROMPT ========================================
PROMPT Fixing SYS_COMPANY Stored Procedures to Match Actual Schema
PROMPT ========================================

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_ALL (Corrected)
-- Description: Retrieves all active companies with correct columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        TAX_NUMBER,
        COUNTRY_ID,
        CURR_ID,
        DEFAULT_BRANCH_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving companies: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_BY_ID (Corrected)
-- Description: Retrieves a specific company by ID with correct columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        TAX_NUMBER,
        COUNTRY_ID,
        CURR_ID,
        DEFAULT_BRANCH_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving company by ID: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT (Corrected)
-- Description: Inserts a new company record with correct columns only
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Generate new ID from sequence
    SELECT SEQ_SYS_COMPANY.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new company record
    INSERT INTO SYS_COMPANY (
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        TAX_NUMBER,
        COUNTRY_ID,
        CURR_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_LEGAL_NAME,
        P_LEGAL_NAME_E,
        P_COMPANY_CODE,
        P_TAX_NUMBER,
        P_COUNTRY_ID,
        P_CURR_ID,
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20203, 'Company code already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20204, 'Error inserting company: ' || SQLERRM);
END SP_SYS_COMPANY_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_UPDATE (Corrected)
-- Description: Updates an existing company record with correct columns only
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_DEFAULT_BRANCH_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the company record
    UPDATE SYS_COMPANY
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        LEGAL_NAME = P_LEGAL_NAME,
        LEGAL_NAME_E = P_LEGAL_NAME_E,
        COMPANY_CODE = P_COMPANY_CODE,
        TAX_NUMBER = P_TAX_NUMBER,
        COUNTRY_ID = P_COUNTRY_ID,
        CURR_ID = P_CURR_ID,
        DEFAULT_BRANCH_ID = P_DEFAULT_BRANCH_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20205, 'No company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20206, 'Company code already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20207, 'Error updating company: ' || SQLERRM);
END SP_SYS_COMPANY_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_SET_DEFAULT_BRANCH
-- Description: Sets the default branch for a company
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SET_DEFAULT_BRANCH (
    P_COMPANY_ID IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
    V_BRANCH_EXISTS NUMBER;
BEGIN
    -- Verify the branch exists and belongs to this company
    SELECT COUNT(*)
    INTO V_BRANCH_EXISTS
    FROM SYS_BRANCH
    WHERE ROW_ID = P_BRANCH_ID
    AND PAR_ROW_ID = P_COMPANY_ID
    AND IS_ACTIVE = '1';
    
    IF V_BRANCH_EXISTS = 0 THEN
        RAISE_APPLICATION_ERROR(-20211, 'Branch does not exist or does not belong to this company');
    END IF;
    
    -- Update the company's default branch
    UPDATE SYS_COMPANY
    SET 
        DEFAULT_BRANCH_ID = P_BRANCH_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_COMPANY_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20212, 'No company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20213, 'Error setting default branch: ' || SQLERRM);
END SP_SYS_COMPANY_SET_DEFAULT_BRANCH;
/

PROMPT ========================================
PROMPT Procedures Fixed Successfully
PROMPT ========================================

-- Verification: Display procedure status
SELECT object_name, object_type, status, last_ddl_time
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_SELECT_ALL',
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_COMPANY_INSERT',
    'SP_SYS_COMPANY_UPDATE',
    'SP_SYS_COMPANY_DELETE',
    'SP_SYS_COMPANY_UPDATE_LOGO',
    'SP_SYS_COMPANY_GET_LOGO',
    'SP_SYS_COMPANY_SET_DEFAULT_BRANCH'
)
ORDER BY object_name;

PROMPT '';
PROMPT 'Columns in SYS_COMPANY table:';
SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
ORDER BY column_id;

PROMPT ========================================
PROMPT Fix Complete
PROMPT ========================================
PROMPT 
PROMPT The procedures now match the actual SYS_COMPANY table schema:
PROMPT - ROW_ID, ROW_DESC, ROW_DESC_E
PROMPT - LEGAL_NAME, LEGAL_NAME_E
PROMPT - COMPANY_CODE, TAX_NUMBER
PROMPT - COUNTRY_ID, CURR_ID
PROMPT - DEFAULT_BRANCH_ID
PROMPT - COMPANY_LOGO (BLOB)
PROMPT - IS_ACTIVE, CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
PROMPT 
PROMPT Removed non-existent columns:
PROMPT - DEFAULT_LANG (moved to SYS_BRANCH)
PROMPT - FISCAL_YEAR_ID (moved to SYS_BRANCH)
PROMPT - BASE_CURRENCY_ID (moved to SYS_BRANCH)
PROMPT - SYSTEM_LANGUAGE (removed)
PROMPT - ROUNDING_RULES (moved to SYS_BRANCH)
PROMPT 
