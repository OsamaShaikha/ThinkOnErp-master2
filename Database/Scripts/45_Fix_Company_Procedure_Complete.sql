-- =============================================
-- Script: 45_Fix_Company_Procedure_Complete.sql
-- Description: Complete fix for SP_SYS_COMPANY_INSERT_WITH_BRANCH to match C# code expectations
-- This script will work regardless of which previous version you have
-- Author: System
-- Date: 2026-04-26
-- =============================================

PROMPT ========================================
PROMPT Fixing SP_SYS_COMPANY_INSERT_WITH_BRANCH Procedure
PROMPT ========================================

-- Drop the existing procedure to ensure clean recreation
DROP PROCEDURE SP_SYS_COMPANY_INSERT_WITH_BRANCH;

PROMPT 'Old procedure dropped. Creating new version...';

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT_WITH_BRANCH
-- Description: Creates a new company, default branch, and default fiscal year
-- 
-- This version matches the C# code expectations:
-- - Company logo support (BLOB)
-- - Branch-level settings (DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES as NUMBER)
-- - Automatic fiscal year creation
-- - Three output parameters: company ID, branch ID, fiscal year ID
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT_WITH_BRANCH (
    -- Company Parameters (in order expected by C# code)
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
    P_COMPANY_LOGO IN BLOB DEFAULT NULL,
    
    -- Branch Parameters (in order expected by C# code)
    P_BRANCH_DESC IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_DESC_E IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_PHONE IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_MOBILE IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_FAX IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_EMAIL IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_LOGO IN BLOB DEFAULT NULL,
    P_DEFAULT_LANG IN VARCHAR2 DEFAULT 'ar',
    P_BASE_CURRENCY_ID IN NUMBER DEFAULT NULL,
    P_ROUNDING_RULES IN NUMBER DEFAULT 1,
    
    -- Common Parameters
    P_CREATION_USER IN VARCHAR2,
    
    -- Output Parameters (in order expected by C# code)
    P_NEW_COMPANY_ID OUT NUMBER,
    P_NEW_BRANCH_ID OUT NUMBER,
    P_NEW_FISCAL_YEAR_ID OUT NUMBER
)
AS
    V_BRANCH_DESC VARCHAR2(200);
    V_BRANCH_DESC_E VARCHAR2(200);
    V_FISCAL_YEAR_CODE VARCHAR2(20);
    V_FISCAL_YEAR_DESC VARCHAR2(200);
    V_FISCAL_YEAR_DESC_E VARCHAR2(200);
    V_CURRENT_YEAR NUMBER;
    V_START_DATE DATE;
    V_END_DATE DATE;
    V_ERROR_MESSAGE VARCHAR2(4000);
BEGIN
    -- Start transaction
    SAVEPOINT company_branch_fiscal_creation;
    
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
    
    -- Validate language parameters
    IF P_DEFAULT_LANG NOT IN ('ar', 'en') THEN
        RAISE_APPLICATION_ERROR(-20305, 'Default language must be ar or en');
    END IF;
    
    -- Validate rounding rules
    IF P_ROUNDING_RULES NOT IN (1, 2, 3, 4, 5, 6) THEN
        RAISE_APPLICATION_ERROR(-20307, 'Invalid rounding rules. Must be one of: 1=HALF_UP, 2=HALF_DOWN, 3=UP, 4=DOWN, 5=CEILING, 6=FLOOR');
    END IF;
    
    -- Check if company code already exists
    DECLARE
        V_COUNT NUMBER;
    BEGIN
        SELECT COUNT(*)
        INTO V_COUNT
        FROM SYS_COMPANY
        WHERE COMPANY_CODE = P_COMPANY_CODE;
        
        IF V_COUNT > 0 THEN
            RAISE_APPLICATION_ERROR(-20308, 'Company code already exists: ' || P_COMPANY_CODE);
        END IF;
    END;
    
    -- Step 1: Create the company
    BEGIN
        -- Generate new company ID from sequence
        SELECT SEQ_SYS_COMPANY.NEXTVAL INTO P_NEW_COMPANY_ID FROM DUAL;
        
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
            COMPANY_LOGO,
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE
        ) VALUES (
            P_NEW_COMPANY_ID,
            NVL(P_ROW_DESC, P_ROW_DESC_E),
            P_ROW_DESC_E,
            P_LEGAL_NAME,
            P_LEGAL_NAME_E,
            P_COMPANY_CODE,
            P_TAX_NUMBER,
            P_COUNTRY_ID,
            P_CURR_ID,
            P_COMPANY_LOGO,
            '1',
            P_CREATION_USER,
            SYSDATE
        );
        
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            ROLLBACK TO company_branch_fiscal_creation;
            RAISE_APPLICATION_ERROR(-20309, 'Company code already exists: ' || P_COMPANY_CODE);
        WHEN OTHERS THEN
            ROLLBACK TO company_branch_fiscal_creation;
            V_ERROR_MESSAGE := 'Error creating company: ' || SQLERRM;
            RAISE_APPLICATION_ERROR(-20310, V_ERROR_MESSAGE);
    END;
    
    -- Step 2: Create the default branch
    BEGIN
        -- Generate branch descriptions if not provided
        IF P_BRANCH_DESC IS NULL THEN
            V_BRANCH_DESC := NVL(P_ROW_DESC, P_ROW_DESC_E) || ' - الفرع الرئيسي';
        ELSE
            V_BRANCH_DESC := P_BRANCH_DESC;
        END IF;
        
        IF P_BRANCH_DESC_E IS NULL THEN
            V_BRANCH_DESC_E := P_ROW_DESC_E || ' - Head Office';
        ELSE
            V_BRANCH_DESC_E := P_BRANCH_DESC_E;
        END IF;
        
        -- Generate new branch ID from sequence
        SELECT SEQ_SYS_BRANCH.NEXTVAL INTO P_NEW_BRANCH_ID FROM DUAL;
        
        -- Insert the new branch record
        INSERT INTO SYS_BRANCH (
            ROW_ID,
            PAR_ROW_ID,
            ROW_DESC,
            ROW_DESC_E,
            PHONE,
            MOBILE,
            FAX,
            EMAIL,
            IS_HEAD_BRANCH,
            DEFAULT_LANG,
            BASE_CURRENCY_ID,
            ROUNDING_RULES,
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE,
            BRANCH_LOGO
        ) VALUES (
            P_NEW_BRANCH_ID,
            P_NEW_COMPANY_ID,
            V_BRANCH_DESC,
            V_BRANCH_DESC_E,
            P_BRANCH_PHONE,
            P_BRANCH_MOBILE,
            P_BRANCH_FAX,
            P_BRANCH_EMAIL,
            '1', -- This is the head branch
            NVL(P_DEFAULT_LANG, 'ar'),
            P_BASE_CURRENCY_ID,
            NVL(P_ROUNDING_RULES, 1),
            '1', -- Active
            P_CREATION_USER,
            SYSDATE,
            P_BRANCH_LOGO
        );
        
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK TO company_branch_fiscal_creation;
            V_ERROR_MESSAGE := 'Error creating default branch: ' || SQLERRM;
            RAISE_APPLICATION_ERROR(-20311, V_ERROR_MESSAGE);
    END;
    
    -- Step 3: Update company with default branch ID
    BEGIN
        UPDATE SYS_COMPANY
        SET DEFAULT_BRANCH_ID = P_NEW_BRANCH_ID
        WHERE ROW_ID = P_NEW_COMPANY_ID;
        
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK TO company_branch_fiscal_creation;
            V_ERROR_MESSAGE := 'Error updating company with default branch: ' || SQLERRM;
            RAISE_APPLICATION_ERROR(-20314, V_ERROR_MESSAGE);
    END;
    
    -- Step 4: Create default fiscal year for the branch
    BEGIN
        -- Get current year
        SELECT EXTRACT(YEAR FROM SYSDATE) INTO V_CURRENT_YEAR FROM DUAL;
        
        -- Generate fiscal year code (e.g., 'FY2026')
        V_FISCAL_YEAR_CODE := 'FY' || V_CURRENT_YEAR;
        
        -- Generate fiscal year descriptions
        V_FISCAL_YEAR_DESC := 'السنة المالية ' || V_CURRENT_YEAR;
        V_FISCAL_YEAR_DESC_E := 'Fiscal Year ' || V_CURRENT_YEAR;
        
        -- Set start and end dates (January 1 to December 31 of current year)
        V_START_DATE := TO_DATE('01-01-' || V_CURRENT_YEAR, 'DD-MM-YYYY');
        V_END_DATE := TO_DATE('31-12-' || V_CURRENT_YEAR, 'DD-MM-YYYY');
        
        -- Generate new fiscal year ID from sequence
        SELECT SEQ_SYS_FISCAL_YEAR.NEXTVAL INTO P_NEW_FISCAL_YEAR_ID FROM DUAL;
        
        -- Insert the new fiscal year record
        INSERT INTO SYS_FISCAL_YEAR (
            ROW_ID,
            COMPANY_ID,
            BRANCH_ID,
            FISCAL_YEAR_CODE,
            ROW_DESC,
            ROW_DESC_E,
            START_DATE,
            END_DATE,
            IS_CLOSED,
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE
        ) VALUES (
            P_NEW_FISCAL_YEAR_ID,
            P_NEW_COMPANY_ID,
            P_NEW_BRANCH_ID,
            V_FISCAL_YEAR_CODE,
            V_FISCAL_YEAR_DESC,
            V_FISCAL_YEAR_DESC_E,
            V_START_DATE,
            V_END_DATE,
            '0', -- Not closed
            '1', -- Active
            P_CREATION_USER,
            SYSDATE
        );
        
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK TO company_branch_fiscal_creation;
            V_ERROR_MESSAGE := 'Error creating default fiscal year: ' || SQLERRM;
            RAISE_APPLICATION_ERROR(-20315, V_ERROR_MESSAGE);
    END;
    
    -- Commit the transaction
    COMMIT;
    
    -- Log success
    DBMS_OUTPUT.PUT_LINE('Company created successfully with ID: ' || P_NEW_COMPANY_ID);
    DBMS_OUTPUT.PUT_LINE('Default branch created successfully with ID: ' || P_NEW_BRANCH_ID);
    DBMS_OUTPUT.PUT_LINE('Default fiscal year created successfully with ID: ' || P_NEW_FISCAL_YEAR_ID);
    DBMS_OUTPUT.PUT_LINE('Fiscal year code: ' || V_FISCAL_YEAR_CODE);
    DBMS_OUTPUT.PUT_LINE('Fiscal year period: ' || TO_CHAR(V_START_DATE, 'DD-MON-YYYY') || ' to ' || TO_CHAR(V_END_DATE, 'DD-MON-YYYY'));
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO company_branch_fiscal_creation;
        V_ERROR_MESSAGE := 'Error in SP_SYS_COMPANY_INSERT_WITH_BRANCH: ' || SQLERRM;
        RAISE_APPLICATION_ERROR(-20312, V_ERROR_MESSAGE);
END SP_SYS_COMPANY_INSERT_WITH_BRANCH;
/

PROMPT ========================================
PROMPT Procedure Created Successfully
PROMPT ========================================

-- Verification: Display procedure status
SELECT object_name, object_type, status, last_ddl_time
FROM user_objects
WHERE object_name = 'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
ORDER BY object_name;

PROMPT '';
PROMPT 'Procedure Parameters:';
SELECT 
    argument_name,
    position,
    data_type,
    in_out
FROM user_arguments
WHERE object_name = 'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
ORDER BY position;

PROMPT ========================================
PROMPT Fix Complete
PROMPT ========================================
PROMPT 
PROMPT The procedure now expects these parameters (in order):
PROMPT 1. P_ROW_DESC (IN VARCHAR2)
PROMPT 2. P_ROW_DESC_E (IN VARCHAR2)
PROMPT 3. P_LEGAL_NAME (IN VARCHAR2)
PROMPT 4. P_LEGAL_NAME_E (IN VARCHAR2)
PROMPT 5. P_COMPANY_CODE (IN VARCHAR2)
PROMPT 6. P_TAX_NUMBER (IN VARCHAR2)
PROMPT 7. P_COUNTRY_ID (IN NUMBER)
PROMPT 8. P_CURR_ID (IN NUMBER)
PROMPT 9. P_COMPANY_LOGO (IN BLOB)
PROMPT 10. P_BRANCH_DESC (IN VARCHAR2)
PROMPT 11. P_BRANCH_DESC_E (IN VARCHAR2)
PROMPT 12. P_BRANCH_PHONE (IN VARCHAR2)
PROMPT 13. P_BRANCH_MOBILE (IN VARCHAR2)
PROMPT 14. P_BRANCH_FAX (IN VARCHAR2)
PROMPT 15. P_BRANCH_EMAIL (IN VARCHAR2)
PROMPT 16. P_BRANCH_LOGO (IN BLOB)
PROMPT 17. P_DEFAULT_LANG (IN VARCHAR2)
PROMPT 18. P_BASE_CURRENCY_ID (IN NUMBER)
PROMPT 19. P_ROUNDING_RULES (IN NUMBER)
PROMPT 20. P_CREATION_USER (IN VARCHAR2)
PROMPT 21. P_NEW_COMPANY_ID (OUT NUMBER)
PROMPT 22. P_NEW_BRANCH_ID (OUT NUMBER)
PROMPT 23. P_NEW_FISCAL_YEAR_ID (OUT NUMBER)
PROMPT 
PROMPT This matches the C# code expectations exactly.
PROMPT 
