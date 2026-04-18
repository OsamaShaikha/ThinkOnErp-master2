-- =============================================
-- ThinkOnErp API - Add Branch Logo Support
-- Description: Adds BRANCH_LOGO column to SYS_BRANCH table and creates logo management procedures
-- Version: 1.0
-- Date: April 17, 2026
-- =============================================

-- =============================================
-- Step 1: Add BRANCH_LOGO column to SYS_BRANCH table
-- =============================================
PROMPT 'Adding BRANCH_LOGO column to SYS_BRANCH table...';

ALTER TABLE SYS_BRANCH ADD (
    BRANCH_LOGO BLOB
);

-- Add comment to the new column
COMMENT ON COLUMN SYS_BRANCH.BRANCH_LOGO IS 'Branch logo image stored as BLOB (max 5MB)';

PROMPT 'BRANCH_LOGO column added successfully to SYS_BRANCH table.';

-- =============================================
-- Step 2: Update SP_SYS_BRANCH_SELECT_ALL to include BRANCH_LOGO
-- =============================================
PROMPT 'Updating SP_SYS_BRANCH_SELECT_ALL procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
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
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving branches: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_ALL;
/

-- =============================================
-- Step 3: Update SP_SYS_BRANCH_SELECT_BY_ID to include BRANCH_LOGO
-- =============================================
PROMPT 'Updating SP_SYS_BRANCH_SELECT_BY_ID procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
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
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving branch by ID: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_BY_ID;
/

-- =============================================
-- Step 4: Create SP_SYS_BRANCH_UPDATE_LOGO procedure
-- =============================================
PROMPT 'Creating SP_SYS_BRANCH_UPDATE_LOGO procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_UPDATE_LOGO (
    P_ROW_ID IN NUMBER,
    P_BRANCH_LOGO IN BLOB,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the branch logo
    UPDATE SYS_BRANCH
    SET 
        BRANCH_LOGO = P_BRANCH_LOGO,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20208, 'No branch found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20209, 'Error updating branch logo: ' || SQLERRM);
END SP_SYS_BRANCH_UPDATE_LOGO;
/

-- =============================================
-- Step 5: Create SP_SYS_BRANCH_GET_LOGO procedure
-- =============================================
PROMPT 'Creating SP_SYS_BRANCH_GET_LOGO procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_GET_LOGO (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        BRANCH_LOGO
    FROM SYS_BRANCH
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20210, 'Error retrieving branch logo: ' || SQLERRM);
END SP_SYS_BRANCH_GET_LOGO;
/

-- =============================================
-- Step 6: Create SP_SYS_BRANCH_SELECT_BY_COMPANY procedure (with logo info)
-- =============================================
PROMPT 'Creating SP_SYS_BRANCH_SELECT_BY_COMPANY procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_BY_COMPANY (
    P_COMPANY_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
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
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE PAR_ROW_ID = P_COMPANY_ID 
      AND IS_ACTIVE = '1'
    ORDER BY IS_HEAD_BRANCH DESC, ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20211, 'Error retrieving branches by company: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_BY_COMPANY;
/

-- =============================================
-- Step 7: Update the company with branch creation procedure to support branch logo
-- =============================================
PROMPT 'Updating SP_SYS_COMPANY_INSERT_WITH_BRANCH to support branch logo...';

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
    P_BRANCH_LOGO IN BLOB DEFAULT NULL,
    
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
            '1', -- Active
            P_CREATION_USER,
            SYSDATE,
            P_BRANCH_LOGO
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
-- Step 8: Verification - Display all created/updated procedures
-- =============================================
PROMPT 'Verifying created/updated procedures...';

SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_BRANCH_SELECT_ALL',
    'SP_SYS_BRANCH_SELECT_BY_ID',
    'SP_SYS_BRANCH_UPDATE_LOGO',
    'SP_SYS_BRANCH_GET_LOGO',
    'SP_SYS_BRANCH_SELECT_BY_COMPANY',
    'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
)
ORDER BY object_name;

-- =============================================
-- Step 9: Grant permissions (adjust as needed for your environment)
-- =============================================
-- GRANT EXECUTE ON SP_SYS_BRANCH_UPDATE_LOGO TO your_application_user;
-- GRANT EXECUTE ON SP_SYS_BRANCH_GET_LOGO TO your_application_user;
-- GRANT EXECUTE ON SP_SYS_BRANCH_SELECT_BY_COMPANY TO your_application_user;

-- =============================================
-- Step 10: Test data verification (optional)
-- =============================================
PROMPT 'Checking SYS_BRANCH table structure...';

SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_BRANCH'
  AND column_name IN ('BRANCH_LOGO', 'ROW_ID', 'PAR_ROW_ID')
ORDER BY column_id;

PROMPT 'Branch logo support implementation completed successfully!';
PROMPT 'New features available:';
PROMPT '- BRANCH_LOGO column added to SYS_BRANCH table';
PROMPT '- SP_SYS_BRANCH_UPDATE_LOGO procedure for logo management';
PROMPT '- SP_SYS_BRANCH_GET_LOGO procedure for logo retrieval';
PROMPT '- SP_SYS_BRANCH_SELECT_BY_COMPANY procedure for company branches';
PROMPT '- Updated company with branch creation to support branch logos';
PROMPT '- HAS_LOGO indicator in branch queries';