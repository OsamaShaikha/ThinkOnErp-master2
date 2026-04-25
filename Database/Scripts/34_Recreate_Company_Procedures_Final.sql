-- =============================================
-- ThinkOnErp API - Recreate Company Procedures (Final Version)
-- Description: Recreates all company stored procedures after field migration and SystemLanguage removal
-- Version: 1.0
-- Date: April 24, 2026
-- Author: ThinkOnErp Development Team
-- =============================================

-- This script recreates all company stored procedures to reflect the final schema:
-- - Removed: BASE_CURRENCY_ID, SYSTEM_LANGUAGE, ROUNDING_RULES (moved to branch level)
-- - Removed: DEFAULT_LANG (moved to branch level)
-- - Kept: All other company-specific fields

PROMPT '=== Recreating Company Stored Procedures (Final Version) ==='
PROMPT 'Script: 34_Recreate_Company_Procedures_Final.sql'
PROMPT 'Purpose: Update all company procedures after field migration'
PROMPT ''

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_ALL (Final Version)
-- Description: Retrieves all active companies (company-level fields only)
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_SELECT_ALL...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        c.ROW_ID,
        c.ROW_DESC,
        c.ROW_DESC_E,
        c.LEGAL_NAME,
        c.LEGAL_NAME_E,
        c.COMPANY_CODE,
        c.TAX_NUMBER,
        c.FISCAL_YEAR_ID,
        fy.FISCAL_YEAR_CODE,
        c.DEFAULT_BRANCH_ID,
        db.ROW_DESC_E AS DEFAULT_BRANCH_NAME,
        c.COUNTRY_ID,
        c.CURR_ID,
        c.IS_ACTIVE,
        c.CREATION_USER,
        c.CREATION_DATE,
        c.UPDATE_USER,
        c.UPDATE_DATE,
        CASE 
            WHEN c.COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY c
    LEFT JOIN SYS_FISCAL_YEAR fy ON c.FISCAL_YEAR_ID = fy.ROW_ID
    LEFT JOIN SYS_BRANCH db ON c.DEFAULT_BRANCH_ID = db.ROW_ID
    WHERE c.IS_ACTIVE = '1'
    ORDER BY c.ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving companies: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_BY_ID (Final Version)
-- Description: Retrieves a specific company by ID (company-level fields only)
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_SELECT_BY_ID...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        c.ROW_ID,
        c.ROW_DESC,
        c.ROW_DESC_E,
        c.LEGAL_NAME,
        c.LEGAL_NAME_E,
        c.COMPANY_CODE,
        c.TAX_NUMBER,
        c.FISCAL_YEAR_ID,
        fy.FISCAL_YEAR_CODE,
        c.DEFAULT_BRANCH_ID,
        db.ROW_DESC_E AS DEFAULT_BRANCH_NAME,
        c.COUNTRY_ID,
        c.CURR_ID,
        c.IS_ACTIVE,
        c.CREATION_USER,
        c.CREATION_DATE,
        c.UPDATE_USER,
        c.UPDATE_DATE,
        CASE 
            WHEN c.COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY c
    LEFT JOIN SYS_FISCAL_YEAR fy ON c.FISCAL_YEAR_ID = fy.ROW_ID
    LEFT JOIN SYS_BRANCH db ON c.DEFAULT_BRANCH_ID = db.ROW_ID
    WHERE c.ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving company by ID: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT (Final Version)
-- Description: Inserts a new company record (company-level fields only)
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_INSERT...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_FISCAL_YEAR_ID IN NUMBER DEFAULT NULL,
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Validate required parameters
    IF P_ROW_DESC_E IS NULL OR LENGTH(TRIM(P_ROW_DESC_E)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20301, 'Company English name is required');
    END IF;
    
    IF P_LEGAL_NAME_E IS NULL OR LENGTH(TRIM(P_LEGAL_NAME_E)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20302, 'Company legal English name is required');
    END IF;
    
    IF P_COMPANY_CODE IS NULL OR LENGTH(TRIM(P_COMPANY_CODE)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20303, 'Company code is required');
    END IF;
    
    IF P_CREATION_USER IS NULL OR LENGTH(TRIM(P_CREATION_USER)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20304, 'Creation user is required');
    END IF;
    
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
        FISCAL_YEAR_ID,
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
        P_FISCAL_YEAR_ID,
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
        RAISE_APPLICATION_ERROR(-20305, 'Company code already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20306, 'Error inserting company: ' || SQLERRM);
END SP_SYS_COMPANY_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_UPDATE (Final Version)
-- Description: Updates an existing company record (company-level fields only)
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_UPDATE...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_FISCAL_YEAR_ID IN NUMBER DEFAULT NULL,
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Validate required parameters
    IF P_ROW_ID IS NULL OR P_ROW_ID <= 0 THEN
        RAISE_APPLICATION_ERROR(-20307, 'Valid company ID is required');
    END IF;
    
    IF P_ROW_DESC_E IS NULL OR LENGTH(TRIM(P_ROW_DESC_E)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20308, 'Company English name is required');
    END IF;
    
    IF P_LEGAL_NAME_E IS NULL OR LENGTH(TRIM(P_LEGAL_NAME_E)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20309, 'Company legal English name is required');
    END IF;
    
    IF P_COMPANY_CODE IS NULL OR LENGTH(TRIM(P_COMPANY_CODE)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20310, 'Company code is required');
    END IF;
    
    IF P_UPDATE_USER IS NULL OR LENGTH(TRIM(P_UPDATE_USER)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20311, 'Update user is required');
    END IF;
    
    -- Update the company record
    UPDATE SYS_COMPANY
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        LEGAL_NAME = P_LEGAL_NAME,
        LEGAL_NAME_E = P_LEGAL_NAME_E,
        COMPANY_CODE = P_COMPANY_CODE,
        TAX_NUMBER = P_TAX_NUMBER,
        FISCAL_YEAR_ID = P_FISCAL_YEAR_ID,
        COUNTRY_ID = P_COUNTRY_ID,
        CURR_ID = P_CURR_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID
    AND IS_ACTIVE = '1';
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20312, 'No active company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20313, 'Company code already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20314, 'Error updating company: ' || SQLERRM);
END SP_SYS_COMPANY_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_DELETE (Final Version)
-- Description: Soft deletes a company (sets IS_ACTIVE = '0')
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_DELETE...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_DELETE (
    P_ROW_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Validate required parameters
    IF P_ROW_ID IS NULL OR P_ROW_ID <= 0 THEN
        RAISE_APPLICATION_ERROR(-20315, 'Valid company ID is required');
    END IF;
    
    IF P_UPDATE_USER IS NULL OR LENGTH(TRIM(P_UPDATE_USER)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20316, 'Update user is required');
    END IF;
    
    -- Soft delete the company record
    UPDATE SYS_COMPANY
    SET 
        IS_ACTIVE = '0',
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID
    AND IS_ACTIVE = '1';
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20317, 'No active company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20318, 'Error deleting company: ' || SQLERRM);
END SP_SYS_COMPANY_DELETE;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_UPDATE_LOGO (Final Version)
-- Description: Updates company logo separately (BLOB handling)
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_UPDATE_LOGO...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE_LOGO (
    P_ROW_ID IN NUMBER,
    P_COMPANY_LOGO IN BLOB,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Validate required parameters
    IF P_ROW_ID IS NULL OR P_ROW_ID <= 0 THEN
        RAISE_APPLICATION_ERROR(-20319, 'Valid company ID is required');
    END IF;
    
    IF P_UPDATE_USER IS NULL OR LENGTH(TRIM(P_UPDATE_USER)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20320, 'Update user is required');
    END IF;
    
    -- Update the company logo
    UPDATE SYS_COMPANY
    SET 
        COMPANY_LOGO = P_COMPANY_LOGO,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID
    AND IS_ACTIVE = '1';
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20321, 'No active company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20322, 'Error updating company logo: ' || SQLERRM);
END SP_SYS_COMPANY_UPDATE_LOGO;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_GET_LOGO (Final Version)
-- Description: Retrieves company logo
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_GET_LOGO...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_GET_LOGO (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        COMPANY_LOGO
    FROM SYS_COMPANY
    WHERE ROW_ID = P_ROW_ID
    AND IS_ACTIVE = '1';
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20323, 'Error retrieving company logo: ' || SQLERRM);
END SP_SYS_COMPANY_GET_LOGO;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_SET_DEFAULT_BRANCH (Final Version)
-- Description: Sets the default branch for a company
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_SET_DEFAULT_BRANCH...'

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SET_DEFAULT_BRANCH (
    P_COMPANY_ID IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
    V_BRANCH_COUNT NUMBER;
BEGIN
    -- Validate required parameters
    IF P_COMPANY_ID IS NULL OR P_COMPANY_ID <= 0 THEN
        RAISE_APPLICATION_ERROR(-20324, 'Valid company ID is required');
    END IF;
    
    IF P_BRANCH_ID IS NULL OR P_BRANCH_ID <= 0 THEN
        RAISE_APPLICATION_ERROR(-20325, 'Valid branch ID is required');
    END IF;
    
    IF P_UPDATE_USER IS NULL OR LENGTH(TRIM(P_UPDATE_USER)) = 0 THEN
        RAISE_APPLICATION_ERROR(-20326, 'Update user is required');
    END IF;
    
    -- Verify that the branch belongs to the company
    SELECT COUNT(*)
    INTO V_BRANCH_COUNT
    FROM SYS_BRANCH
    WHERE ROW_ID = P_BRANCH_ID
    AND PAR_ROW_ID = P_COMPANY_ID
    AND IS_ACTIVE = '1';
    
    IF V_BRANCH_COUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20327, 'Branch does not belong to the specified company or is not active');
    END IF;
    
    -- Update the company's default branch
    UPDATE SYS_COMPANY
    SET 
        DEFAULT_BRANCH_ID = P_BRANCH_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_COMPANY_ID
    AND IS_ACTIVE = '1';
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20328, 'No active company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20329, 'Error setting default branch: ' || SQLERRM);
END SP_SYS_COMPANY_SET_DEFAULT_BRANCH;
/

-- =============================================
-- Verification: Display all updated procedures
-- =============================================
PROMPT ''
PROMPT 'Verifying created procedures...'

SELECT 
    object_name,
    object_type,
    status,
    created,
    last_ddl_time
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

-- =============================================
-- Test the procedures (optional)
-- =============================================
PROMPT ''
PROMPT 'Testing procedures...'

DECLARE
    v_cursor SYS_REFCURSOR;
    v_count NUMBER;
BEGIN
    -- Test SP_SYS_COMPANY_SELECT_ALL
    SP_SYS_COMPANY_SELECT_ALL(v_cursor);
    DBMS_OUTPUT.PUT_LINE('✓ SP_SYS_COMPANY_SELECT_ALL executed successfully');
    CLOSE v_cursor;
    
    -- Count companies
    SELECT COUNT(*) INTO v_count FROM SYS_COMPANY WHERE IS_ACTIVE = '1';
    DBMS_OUTPUT.PUT_LINE('✓ Found ' || v_count || ' active companies');
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('✗ Error testing procedures: ' || SQLERRM);
END;
/

PROMPT ''
PROMPT '=== Company Procedures Recreation Completed Successfully ==='
PROMPT 'All company stored procedures have been updated to reflect the final schema:'
PROMPT '- Removed fields moved to branch level: DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES'
PROMPT '- Removed SystemLanguage field completely'
PROMPT '- Added proper validation and error handling'
PROMPT '- Added DEFAULT_BRANCH_ID support with SET_DEFAULT_BRANCH procedure'
PROMPT '- All procedures now work with the current company table structure'
PROMPT ''

-- Commit all changes
COMMIT;