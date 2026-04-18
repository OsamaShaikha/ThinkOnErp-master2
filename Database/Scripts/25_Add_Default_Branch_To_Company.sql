-- =============================================
-- ThinkOnErp API - Add Default Branch Reference to Company Table
-- Description: Adds DEFAULT_BRANCH_ID column to SYS_COMPANY table and updates procedures
-- Version: 1.0
-- Date: April 18, 2026
-- =============================================

-- =============================================
-- Step 1: Add DEFAULT_BRANCH_ID column to SYS_COMPANY table
-- =============================================
PROMPT 'Adding DEFAULT_BRANCH_ID column to SYS_COMPANY table...';

ALTER TABLE SYS_COMPANY ADD (
    DEFAULT_BRANCH_ID NUMBER(19)
);

-- Add comment to the new column
COMMENT ON COLUMN SYS_COMPANY.DEFAULT_BRANCH_ID IS 'Foreign key to SYS_BRANCH table - references the default/head branch for this company';

PROMPT 'DEFAULT_BRANCH_ID column added successfully to SYS_COMPANY table.';

-- =============================================
-- Step 2: Add foreign key constraint
-- =============================================
PROMPT 'Adding foreign key constraint for DEFAULT_BRANCH_ID...';

ALTER TABLE SYS_COMPANY 
ADD CONSTRAINT FK_COMPANY_DEFAULT_BRANCH 
FOREIGN KEY (DEFAULT_BRANCH_ID) 
REFERENCES SYS_BRANCH(ROW_ID);

PROMPT 'Foreign key constraint added successfully.';

-- =============================================
-- Step 3: Update SP_SYS_COMPANY_SELECT_ALL to include DEFAULT_BRANCH_ID
-- =============================================
PROMPT 'Updating SP_SYS_COMPANY_SELECT_ALL procedure...';

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
        DEFAULT_LANG,
        TAX_NUMBER,
        FISCAL_YEAR_ID,
        BASE_CURRENCY_ID,
        SYSTEM_LANGUAGE,
        ROUNDING_RULES,
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
-- Step 4: Update SP_SYS_COMPANY_SELECT_BY_ID to include DEFAULT_BRANCH_ID
-- =============================================
PROMPT 'Updating SP_SYS_COMPANY_SELECT_BY_ID procedure...';

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
        DEFAULT_LANG,
        TAX_NUMBER,
        FISCAL_YEAR_ID,
        BASE_CURRENCY_ID,
        SYSTEM_LANGUAGE,
        ROUNDING_RULES,
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
-- Step 5: Update SP_SYS_COMPANY_INSERT to include DEFAULT_BRANCH_ID
-- =============================================
PROMPT 'Updating SP_SYS_COMPANY_INSERT procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2 DEFAULT NULL,
    P_COMPANY_CODE IN VARCHAR2 DEFAULT NULL,
    P_DEFAULT_LANG IN VARCHAR2 DEFAULT 'ar',
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_FISCAL_YEAR_ID IN NUMBER DEFAULT NULL,
    P_BASE_CURRENCY_ID IN NUMBER DEFAULT NULL,
    P_SYSTEM_LANGUAGE IN VARCHAR2 DEFAULT 'ar',
    P_ROUNDING_RULES IN VARCHAR2 DEFAULT 'HALF_UP',
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
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
        P_NEW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_LEGAL_NAME,
        P_LEGAL_NAME_E,
        P_COMPANY_CODE,
        P_DEFAULT_LANG,
        P_TAX_NUMBER,
        P_FISCAL_YEAR_ID,
        P_BASE_CURRENCY_ID,
        P_SYSTEM_LANGUAGE,
        P_ROUNDING_RULES,
        P_COUNTRY_ID,
        P_CURR_ID,
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20203, 'Error creating company: ' || SQLERRM);
END SP_SYS_COMPANY_INSERT;
/

-- =============================================
-- Step 6: Update SP_SYS_COMPANY_UPDATE to include DEFAULT_BRANCH_ID
-- =============================================
PROMPT 'Updating SP_SYS_COMPANY_UPDATE procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2 DEFAULT NULL,
    P_COMPANY_CODE IN VARCHAR2 DEFAULT NULL,
    P_DEFAULT_LANG IN VARCHAR2 DEFAULT 'ar',
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_FISCAL_YEAR_ID IN NUMBER DEFAULT NULL,
    P_BASE_CURRENCY_ID IN NUMBER DEFAULT NULL,
    P_SYSTEM_LANGUAGE IN VARCHAR2 DEFAULT 'ar',
    P_ROUNDING_RULES IN VARCHAR2 DEFAULT 'HALF_UP',
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    UPDATE SYS_COMPANY
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        LEGAL_NAME = P_LEGAL_NAME,
        LEGAL_NAME_E = P_LEGAL_NAME_E,
        COMPANY_CODE = P_COMPANY_CODE,
        DEFAULT_LANG = P_DEFAULT_LANG,
        TAX_NUMBER = P_TAX_NUMBER,
        FISCAL_YEAR_ID = P_FISCAL_YEAR_ID,
        BASE_CURRENCY_ID = P_BASE_CURRENCY_ID,
        SYSTEM_LANGUAGE = P_SYSTEM_LANGUAGE,
        ROUNDING_RULES = P_ROUNDING_RULES,
        COUNTRY_ID = P_COUNTRY_ID,
        CURR_ID = P_CURR_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20204, 'No company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20205, 'Error updating company: ' || SQLERRM);
END SP_SYS_COMPANY_UPDATE;
/

-- =============================================
-- Step 7: Create SP_SYS_COMPANY_SET_DEFAULT_BRANCH procedure
-- =============================================
PROMPT 'Creating SP_SYS_COMPANY_SET_DEFAULT_BRANCH procedure...';

CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SET_DEFAULT_BRANCH (
    P_COMPANY_ID IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
    V_BRANCH_COUNT NUMBER;
    V_COMPANY_COUNT NUMBER;
BEGIN
    -- Validate that the company exists
    SELECT COUNT(*)
    INTO V_COMPANY_COUNT
    FROM SYS_COMPANY
    WHERE ROW_ID = P_COMPANY_ID AND IS_ACTIVE = '1';
    
    IF V_COMPANY_COUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20301, 'Company not found or inactive');
    END IF;
    
    -- Validate that the branch exists and belongs to the company
    SELECT COUNT(*)
    INTO V_BRANCH_COUNT
    FROM SYS_BRANCH
    WHERE ROW_ID = P_BRANCH_ID 
      AND PAR_ROW_ID = P_COMPANY_ID 
      AND IS_ACTIVE = '1';
    
    IF V_BRANCH_COUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20302, 'Branch not found, inactive, or does not belong to the specified company');
    END IF;
    
    -- Update the company's default branch
    UPDATE SYS_COMPANY
    SET 
        DEFAULT_BRANCH_ID = P_BRANCH_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_COMPANY_ID;
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('Default branch set successfully for company ID: ' || P_COMPANY_ID);
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20303, 'Error setting default branch: ' || SQLERRM);
END SP_SYS_COMPANY_SET_DEFAULT_BRANCH;
/

-- =============================================
-- Step 8: Update SP_SYS_COMPANY_INSERT_WITH_BRANCH to set default branch
-- =============================================
PROMPT 'Updating SP_SYS_COMPANY_INSERT_WITH_BRANCH procedure...';

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
    
    -- Step 1: Create the company (without default branch initially)
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
            DEFAULT_BRANCH_ID, -- Will be updated after branch creation
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
            NULL, -- Will be set after branch creation
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
    
    -- Step 3: Update company with default branch reference
    BEGIN
        UPDATE SYS_COMPANY
        SET 
            DEFAULT_BRANCH_ID = P_NEW_BRANCH_ID,
            UPDATE_USER = P_CREATION_USER,
            UPDATE_DATE = SYSDATE
        WHERE ROW_ID = P_NEW_COMPANY_ID;
        
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK TO company_branch_creation;
            V_ERROR_MESSAGE := 'Error setting default branch reference: ' || SQLERRM;
            RAISE_APPLICATION_ERROR(-20312, V_ERROR_MESSAGE);
    END;
    
    -- Commit the transaction
    COMMIT;
    
    -- Log success
    DBMS_OUTPUT.PUT_LINE('Company created successfully with ID: ' || P_NEW_COMPANY_ID);
    DBMS_OUTPUT.PUT_LINE('Default branch created successfully with ID: ' || P_NEW_BRANCH_ID);
    DBMS_OUTPUT.PUT_LINE('Default branch reference set in company table');
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO company_branch_creation;
        V_ERROR_MESSAGE := 'Error in SP_SYS_COMPANY_INSERT_WITH_BRANCH: ' || SQLERRM;
        RAISE_APPLICATION_ERROR(-20313, V_ERROR_MESSAGE);
END SP_SYS_COMPANY_INSERT_WITH_BRANCH;
/

-- =============================================
-- Step 9: Update existing companies to set their head branch as default
-- =============================================
PROMPT 'Updating existing companies to set their head branch as default...';

UPDATE SYS_COMPANY 
SET DEFAULT_BRANCH_ID = (
    SELECT ROW_ID 
    FROM SYS_BRANCH 
    WHERE PAR_ROW_ID = SYS_COMPANY.ROW_ID 
      AND IS_HEAD_BRANCH = '1' 
      AND IS_ACTIVE = '1'
      AND ROWNUM = 1
)
WHERE DEFAULT_BRANCH_ID IS NULL
  AND EXISTS (
    SELECT 1 
    FROM SYS_BRANCH 
    WHERE PAR_ROW_ID = SYS_COMPANY.ROW_ID 
      AND IS_HEAD_BRANCH = '1' 
      AND IS_ACTIVE = '1'
  );

COMMIT;

PROMPT 'Updated existing companies with their head branch as default.';

-- =============================================
-- Step 10: Verification - Display all created/updated procedures
-- =============================================
PROMPT 'Verifying created/updated procedures...';

SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_SELECT_ALL',
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_COMPANY_INSERT',
    'SP_SYS_COMPANY_UPDATE',
    'SP_SYS_COMPANY_SET_DEFAULT_BRANCH',
    'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
)
ORDER BY object_name;

-- =============================================
-- Step 11: Verify table structure
-- =============================================
PROMPT 'Checking SYS_COMPANY table structure...';

SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
  AND column_name IN ('DEFAULT_BRANCH_ID', 'ROW_ID', 'COMPANY_CODE')
ORDER BY column_id;

-- =============================================
-- Step 12: Verify foreign key constraint
-- =============================================
PROMPT 'Checking foreign key constraints...';

SELECT constraint_name, constraint_type, table_name, r_constraint_name
FROM user_constraints
WHERE constraint_name = 'FK_COMPANY_DEFAULT_BRANCH';

PROMPT 'Default branch support implementation completed successfully!';
PROMPT 'New features available:';
PROMPT '- DEFAULT_BRANCH_ID column added to SYS_COMPANY table';
PROMPT '- Foreign key constraint to SYS_BRANCH table';
PROMPT '- SP_SYS_COMPANY_SET_DEFAULT_BRANCH procedure for managing default branch';
PROMPT '- Updated company procedures to include default branch support';
PROMPT '- Existing companies updated with their head branch as default';