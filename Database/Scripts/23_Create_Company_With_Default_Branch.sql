-- =============================================
-- ThinkOnErp API - Create Company with Default Branch
-- Description: Stored procedure to create a company and automatically create a default branch
-- Version: 1.0
-- Date: April 17, 2026
-- =============================================

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT_WITH_BRANCH
-- Description: Creates a new company and automatically creates a default head branch
-- Parameters:
--   Company Parameters:
--   P_ROW_DESC: Arabic description of the company
--   P_ROW_DESC_E: English description of the company
--   P_LEGAL_NAME: Legal name in Arabic (optional)
--   P_LEGAL_NAME_E: Legal name in English (required)
--   P_COMPANY_CODE: Unique company code (required)
--   P_DEFAULT_LANG: Default language (ar/en, defaults to 'ar')
--   P_TAX_NUMBER: Tax registration number (optional)
--   P_FISCAL_YEAR_ID: Current fiscal year ID (optional)
--   P_BASE_CURRENCY_ID: Base currency ID (optional)
--   P_SYSTEM_LANGUAGE: System language (ar/en, defaults to 'ar')
--   P_ROUNDING_RULES: Rounding rules (defaults to 'HALF_UP')
--   P_COUNTRY_ID: Country ID (optional)
--   P_CURR_ID: Currency ID (optional)
--   
--   Branch Parameters:
--   P_BRANCH_DESC: Arabic description of the branch (optional, defaults to company name + " - الفرع الرئيسي")
--   P_BRANCH_DESC_E: English description of the branch (optional, defaults to company name + " - Head Office")
--   P_BRANCH_PHONE: Branch phone number (optional)
--   P_BRANCH_MOBILE: Branch mobile number (optional)
--   P_BRANCH_FAX: Branch fax number (optional)
--   P_BRANCH_EMAIL: Branch email address (optional)
--   
--   Common Parameters:
--   P_CREATION_USER: User creating the records
--   
--   Output Parameters:
--   P_NEW_COMPANY_ID: Returns the new company ID
--   P_NEW_BRANCH_ID: Returns the new branch ID
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT_WITH_BRANCH (
    -- Company Parameters
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_DEFAULT_LANG IN VARCHAR2 DEFAULT 'ar',
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_FISCAL_YEAR_ID IN NUMBER DEFAULT NULL,
    P_BASE_CURRENCY_ID IN NUMBER DEFAULT NULL,
    P_SYSTEM_LANGUAGE IN VARCHAR2 DEFAULT 'ar',
    P_ROUNDING_RULES IN VARCHAR2 DEFAULT 'HALF_UP',
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
    
    -- Branch Parameters
    P_BRANCH_DESC IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_DESC_E IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_PHONE IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_MOBILE IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_FAX IN VARCHAR2 DEFAULT NULL,
    P_BRANCH_EMAIL IN VARCHAR2 DEFAULT NULL,
    
    -- Common Parameters
    P_CREATION_USER IN VARCHAR2,
    
    -- Output Parameters
    P_NEW_COMPANY_ID OUT NUMBER,
    P_NEW_BRANCH_ID OUT NUMBER
)
AS
    V_BRANCH_DESC VARCHAR2(200);
    V_BRANCH_DESC_E VARCHAR2(200);
    V_ERROR_MESSAGE VARCHAR2(4000);
BEGIN
    -- Start transaction
    SAVEPOINT company_branch_creation;
    
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
    
    IF P_SYSTEM_LANGUAGE NOT IN ('ar', 'en') THEN
        RAISE_APPLICATION_ERROR(-20306, 'System language must be ar or en');
    END IF;
    
    -- Validate rounding rules
    IF P_ROUNDING_RULES NOT IN ('HALF_UP', 'HALF_DOWN', 'UP', 'DOWN', 'CEILING', 'FLOOR') THEN
        RAISE_APPLICATION_ERROR(-20307, 'Invalid rounding rules. Must be one of: HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR');
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
            DEFAULT_LANG,
            TAX_NUMBER,
            FISCAL_YEAR_ID,
            BASE_CURRENCY_ID,
            SYSTEM_LANGUAGE,
            ROUNDING_RULES,
            COUNTRY_ID,
            CURR_ID,
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
            NVL(P_DEFAULT_LANG, 'ar'),
            P_TAX_NUMBER,
            P_FISCAL_YEAR_ID,
            P_BASE_CURRENCY_ID,
            NVL(P_SYSTEM_LANGUAGE, 'ar'),
            NVL(P_ROUNDING_RULES, 'HALF_UP'),
            P_COUNTRY_ID,
            P_CURR_ID,
            '1',
            P_CREATION_USER,
            SYSDATE
        );
        
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            ROLLBACK TO company_branch_creation;
            RAISE_APPLICATION_ERROR(-20309, 'Company code already exists: ' || P_COMPANY_CODE);
        WHEN OTHERS THEN
            ROLLBACK TO company_branch_creation;
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
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE
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
            '1', -- Active
            P_CREATION_USER,
            SYSDATE
        );
        
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK TO company_branch_creation;
            V_ERROR_MESSAGE := 'Error creating default branch: ' || SQLERRM;
            RAISE_APPLICATION_ERROR(-20311, V_ERROR_MESSAGE);
    END;
    
    -- Commit the transaction
    COMMIT;
    
    -- Log success
    DBMS_OUTPUT.PUT_LINE('Company created successfully with ID: ' || P_NEW_COMPANY_ID);
    DBMS_OUTPUT.PUT_LINE('Default branch created successfully with ID: ' || P_NEW_BRANCH_ID);
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO company_branch_creation;
        V_ERROR_MESSAGE := 'Error in SP_SYS_COMPANY_INSERT_WITH_BRANCH: ' || SQLERRM;
        RAISE_APPLICATION_ERROR(-20312, V_ERROR_MESSAGE);
END SP_SYS_COMPANY_INSERT_WITH_BRANCH;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH
-- Description: Simplified version - creates company with minimal branch info
-- Parameters:
--   P_ROW_DESC: Arabic description of the company
--   P_ROW_DESC_E: English description of the company
--   P_LEGAL_NAME_E: Legal name in English (required)
--   P_COMPANY_CODE: Unique company code (required)
--   P_CREATION_USER: User creating the records
--   P_NEW_COMPANY_ID: Returns the new company ID
--   P_NEW_BRANCH_ID: Returns the new branch ID
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_COMPANY_ID OUT NUMBER,
    P_NEW_BRANCH_ID OUT NUMBER
)
AS
BEGIN
    -- Call the full procedure with default values
    SP_SYS_COMPANY_INSERT_WITH_BRANCH(
        P_ROW_DESC => P_ROW_DESC,
        P_ROW_DESC_E => P_ROW_DESC_E,
        P_LEGAL_NAME => NULL,
        P_LEGAL_NAME_E => P_LEGAL_NAME_E,
        P_COMPANY_CODE => P_COMPANY_CODE,
        P_DEFAULT_LANG => 'ar',
        P_TAX_NUMBER => NULL,
        P_FISCAL_YEAR_ID => NULL,
        P_BASE_CURRENCY_ID => NULL,
        P_SYSTEM_LANGUAGE => 'ar',
        P_ROUNDING_RULES => 'HALF_UP',
        P_COUNTRY_ID => NULL,
        P_CURR_ID => NULL,
        P_BRANCH_DESC => NULL,
        P_BRANCH_DESC_E => NULL,
        P_BRANCH_PHONE => NULL,
        P_BRANCH_MOBILE => NULL,
        P_BRANCH_FAX => NULL,
        P_BRANCH_EMAIL => NULL,
        P_CREATION_USER => P_CREATION_USER,
        P_NEW_COMPANY_ID => P_NEW_COMPANY_ID,
        P_NEW_BRANCH_ID => P_NEW_BRANCH_ID
    );
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20313, 'Error in SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH: ' || SQLERRM);
END SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH;
/

-- =============================================
-- Test the procedures (optional - comment out for production)
-- =============================================
/*
DECLARE
    V_COMPANY_ID NUMBER;
    V_BRANCH_ID NUMBER;
BEGIN
    -- Test the simple procedure
    SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH(
        P_ROW_DESC => 'شركة الاختبار',
        P_ROW_DESC_E => 'Test Company',
        P_LEGAL_NAME_E => 'Test Company LLC',
        P_COMPANY_CODE => 'TEST001',
        P_CREATION_USER => 'system',
        P_NEW_COMPANY_ID => V_COMPANY_ID,
        P_NEW_BRANCH_ID => V_BRANCH_ID
    );
    
    DBMS_OUTPUT.PUT_LINE('Test completed successfully');
    DBMS_OUTPUT.PUT_LINE('Company ID: ' || V_COMPANY_ID);
    DBMS_OUTPUT.PUT_LINE('Branch ID: ' || V_BRANCH_ID);
    
    -- Clean up test data
    DELETE FROM SYS_BRANCH WHERE ROW_ID = V_BRANCH_ID;
    DELETE FROM SYS_COMPANY WHERE ROW_ID = V_COMPANY_ID;
    COMMIT;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Test failed: ' || SQLERRM);
        ROLLBACK;
END;
/
*/

-- =============================================
-- Verification: Display created procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_INSERT_WITH_BRANCH',
    'SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH'
)
ORDER BY object_name;

-- =============================================
-- Grant permissions (adjust as needed for your environment)
-- =============================================
-- GRANT EXECUTE ON SP_SYS_COMPANY_INSERT_WITH_BRANCH TO your_application_user;
-- GRANT EXECUTE ON SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH TO your_application_user;

PROMPT 'Company with Default Branch procedures created successfully!';
PROMPT 'Use SP_SYS_COMPANY_INSERT_WITH_BRANCH for full control';
PROMPT 'Use SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH for simple creation';