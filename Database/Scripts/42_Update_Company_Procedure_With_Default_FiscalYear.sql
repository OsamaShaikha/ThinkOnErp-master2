-- =============================================
-- Script: 42_Update_Company_Procedure_With_Default_FiscalYear.sql
-- Description: Update SP_SYS_COMPANY_INSERT_WITH_BRANCH to automatically create default fiscal year
-- Author: System
-- Date: 2026-04-26
-- =============================================

PROMPT ========================================
PROMPT Updating Company Creation Procedure to Include Default Fiscal Year
PROMPT ========================================

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT_WITH_BRANCH
-- Description: Creates a new company, default branch, and default fiscal year
-- 
-- Changes from previous version:
-- - Automatically creates a default fiscal year for the new branch
-- - Fiscal year starts on January 1st of current year
-- - Fiscal year ends on December 31st of current year
-- - Fiscal year code is generated as 'FY' + current year (e.g., 'FY2026')
-- - Returns the new fiscal year ID as an output parameter
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT_WITH_BRANCH (
    -- Company Parameters
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
    P_COMPANY_LOGO IN BLOB DEFAULT NULL,
    
    -- Branch Parameters (now includes the migrated fields)
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
    
    -- Output Parameters
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
    
    -- Step 1: Create the company (without the migrated fields)
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
    
    -- Step 2: Create the default branch (with the migrated fields)
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
        
        -- Insert the new branch record (with migrated fields)
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
PROMPT Procedure Updated Successfully
PROMPT ========================================

-- Verification: Display updated procedure
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name = 'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
ORDER BY object_name;

PROMPT 
PROMPT Summary of changes:
PROMPT - Added P_NEW_FISCAL_YEAR_ID output parameter
PROMPT - Automatically creates default fiscal year with:
PROMPT   * Code: FY + current year (e.g., FY2026)
PROMPT   * Start Date: January 1st of current year
PROMPT   * End Date: December 31st of current year
PROMPT   * Status: Active and not closed
PROMPT - Fiscal year is associated with both company and branch
PROMPT 
PROMPT Usage Example:
PROMPT DECLARE
PROMPT     V_COMPANY_ID NUMBER;
PROMPT     V_BRANCH_ID NUMBER;
PROMPT     V_FISCAL_YEAR_ID NUMBER;
PROMPT BEGIN
PROMPT     SP_SYS_COMPANY_INSERT_WITH_BRANCH(
PROMPT         P_ROW_DESC => 'شركة الاختبار',
PROMPT         P_ROW_DESC_E => 'Test Company',
PROMPT         P_LEGAL_NAME_E => 'Test Company LLC',
PROMPT         P_COMPANY_CODE => 'TEST001',
PROMPT         P_CREATION_USER => 'admin',
PROMPT         P_NEW_COMPANY_ID => V_COMPANY_ID,
PROMPT         P_NEW_BRANCH_ID => V_BRANCH_ID,
PROMPT         P_NEW_FISCAL_YEAR_ID => V_FISCAL_YEAR_ID
PROMPT     );
PROMPT     DBMS_OUTPUT.PUT_LINE('Company ID: ' || V_COMPANY_ID);
PROMPT     DBMS_OUTPUT.PUT_LINE('Branch ID: ' || V_BRANCH_ID);
PROMPT     DBMS_OUTPUT.PUT_LINE('Fiscal Year ID: ' || V_FISCAL_YEAR_ID);
PROMPT END;
PROMPT /

