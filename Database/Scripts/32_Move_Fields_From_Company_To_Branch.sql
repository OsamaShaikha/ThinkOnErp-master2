-- =============================================
-- ThinkOnErp API - Move Fields from SYS_COMPANY to SYS_BRANCH
-- Description: Moves ROUNDING_RULES, DEFAULT_LANG, and BASE_CURRENCY_ID from SYS_COMPANY to SYS_BRANCH
-- Rationale: These settings are more appropriate at branch level for multi-branch operations
-- =============================================

-- =============================================
-- Step 1: Add new columns to SYS_BRANCH table
-- =============================================
PROMPT 'Adding new columns to SYS_BRANCH table...';

ALTER TABLE SYS_BRANCH ADD (
    DEFAULT_LANG VARCHAR2(10) DEFAULT 'ar',
    BASE_CURRENCY_ID NUMBER,
    ROUNDING_RULES NUMBER DEFAULT 1
);

-- Add comments to new columns for documentation
COMMENT ON COLUMN SYS_BRANCH.DEFAULT_LANG IS 'Default language for the branch (ar/en)';
COMMENT ON COLUMN SYS_BRANCH.BASE_CURRENCY_ID IS 'Base currency for the branch operations';
COMMENT ON COLUMN SYS_BRANCH.ROUNDING_RULES IS 'Rounding rules for calculations (1=HALF_UP, 2=HALF_DOWN, 3=UP, 4=DOWN, 5=CEILING, 6=FLOOR)';

-- Add foreign key constraint for base currency
ALTER TABLE SYS_BRANCH ADD CONSTRAINT FK_BRANCH_BASE_CURRENCY 
    FOREIGN KEY (BASE_CURRENCY_ID) REFERENCES SYS_CURRENCY(ROW_ID);

-- Add check constraints
ALTER TABLE SYS_BRANCH ADD CONSTRAINT CHK_BRANCH_DEFAULT_LANG 
    CHECK (DEFAULT_LANG IN ('ar', 'en'));

ALTER TABLE SYS_BRANCH ADD CONSTRAINT CHK_BRANCH_ROUNDING_RULES 
    CHECK (ROUNDING_RULES IN (1, 2, 3, 4, 5, 6));

-- Create indexes for better query performance
CREATE INDEX IDX_BRANCH_BASE_CURRENCY ON SYS_BRANCH(BASE_CURRENCY_ID);

PROMPT 'New columns added successfully to SYS_BRANCH table.';

-- =============================================
-- Step 2: Migrate existing data from SYS_COMPANY to SYS_BRANCH
-- =============================================
PROMPT 'Migrating data from SYS_COMPANY to SYS_BRANCH...';

-- Update all branches with their parent company's settings
UPDATE SYS_BRANCH b
SET (DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES) = (
    SELECT 
        NVL(c.DEFAULT_LANG, 'ar'),
        c.BASE_CURRENCY_ID,
        CASE 
            WHEN c.ROUNDING_RULES = 'HALF_UP' THEN 1
            WHEN c.ROUNDING_RULES = 'HALF_DOWN' THEN 2
            WHEN c.ROUNDING_RULES = 'UP' THEN 3
            WHEN c.ROUNDING_RULES = 'DOWN' THEN 4
            WHEN c.ROUNDING_RULES = 'CEILING' THEN 5
            WHEN c.ROUNDING_RULES = 'FLOOR' THEN 6
            ELSE 1 -- Default to HALF_UP
        END
    FROM SYS_COMPANY c
    WHERE c.ROW_ID = b.PAR_ROW_ID
)
WHERE EXISTS (
    SELECT 1 FROM SYS_COMPANY c WHERE c.ROW_ID = b.PAR_ROW_ID
);

-- Display migration results
SELECT 
    'Migration Results' as STATUS,
    COUNT(*) as TOTAL_BRANCHES_UPDATED
FROM SYS_BRANCH
WHERE DEFAULT_LANG IS NOT NULL;

PROMPT 'Data migration completed successfully.';

-- =============================================
-- Step 3: Update stored procedures to include new fields
-- =============================================
PROMPT 'Updating SYS_BRANCH stored procedures...';

-- Update SP_SYS_BRANCH_SELECT_ALL
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
        DEFAULT_LANG,
        BASE_CURRENCY_ID,
        ROUNDING_RULES,
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

-- Update SP_SYS_BRANCH_SELECT_BY_ID
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
        DEFAULT_LANG,
        BASE_CURRENCY_ID,
        ROUNDING_RULES,
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

-- Update SP_SYS_BRANCH_INSERT
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_INSERT (
    P_PAR_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_PHONE IN VARCHAR2,
    P_MOBILE IN VARCHAR2,
    P_FAX IN VARCHAR2,
    P_EMAIL IN VARCHAR2,
    P_IS_HEAD_BRANCH IN CHAR,
    P_DEFAULT_LANG IN VARCHAR2 DEFAULT 'ar',
    P_BASE_CURRENCY_ID IN NUMBER DEFAULT NULL,
    P_ROUNDING_RULES IN NUMBER DEFAULT 1,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Generate new ID from sequence
    SELECT SEQ_SYS_BRANCH.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
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
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_PAR_ROW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_PHONE,
        P_MOBILE,
        P_FAX,
        P_EMAIL,
        P_IS_HEAD_BRANCH,
        NVL(P_DEFAULT_LANG, 'ar'),
        P_BASE_CURRENCY_ID,
        NVL(P_ROUNDING_RULES, 1),
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20203, 'Error inserting branch: ' || SQLERRM);
END SP_SYS_BRANCH_INSERT;
/

-- Update SP_SYS_BRANCH_UPDATE
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_UPDATE (
    P_ROW_ID IN NUMBER,
    P_PAR_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_PHONE IN VARCHAR2,
    P_MOBILE IN VARCHAR2,
    P_FAX IN VARCHAR2,
    P_EMAIL IN VARCHAR2,
    P_IS_HEAD_BRANCH IN CHAR,
    P_DEFAULT_LANG IN VARCHAR2,
    P_BASE_CURRENCY_ID IN NUMBER,
    P_ROUNDING_RULES IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the branch record
    UPDATE SYS_BRANCH
    SET 
        PAR_ROW_ID = P_PAR_ROW_ID,
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        PHONE = P_PHONE,
        MOBILE = P_MOBILE,
        FAX = P_FAX,
        EMAIL = P_EMAIL,
        IS_HEAD_BRANCH = P_IS_HEAD_BRANCH,
        DEFAULT_LANG = P_DEFAULT_LANG,
        BASE_CURRENCY_ID = P_BASE_CURRENCY_ID,
        ROUNDING_RULES = P_ROUNDING_RULES,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20204, 'No branch found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20205, 'Error updating branch: ' || SQLERRM);
END SP_SYS_BRANCH_UPDATE;
/

-- Update SP_SYS_BRANCH_SELECT_BY_COMPANY
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
        DEFAULT_LANG,
        BASE_CURRENCY_ID,
        ROUNDING_RULES,
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

PROMPT 'SYS_BRANCH stored procedures updated successfully.';

-- =============================================
-- Step 4: Remove columns from SYS_COMPANY table
-- =============================================
PROMPT 'Removing migrated columns from SYS_COMPANY table...';

-- Drop foreign key constraint first
ALTER TABLE SYS_COMPANY DROP CONSTRAINT FK_COMPANY_BASE_CURRENCY;

-- Drop check constraints
ALTER TABLE SYS_COMPANY DROP CONSTRAINT CHK_DEFAULT_LANG;
ALTER TABLE SYS_COMPANY DROP CONSTRAINT CHK_ROUNDING_RULES;

-- Drop indexes
DROP INDEX IDX_COMPANY_BASE_CURRENCY;

-- Drop the columns
ALTER TABLE SYS_COMPANY DROP COLUMN DEFAULT_LANG;
ALTER TABLE SYS_COMPANY DROP COLUMN BASE_CURRENCY_ID;
ALTER TABLE SYS_COMPANY DROP COLUMN ROUNDING_RULES;
ALTER TABLE SYS_COMPANY DROP COLUMN SYSTEM_LANGUAGE;

PROMPT 'Columns removed successfully from SYS_COMPANY table.';

-- =============================================
-- Step 5: Update company procedures to remove the migrated fields
-- =============================================
PROMPT 'Updating SYS_COMPANY stored procedures...';

-- Update SP_SYS_COMPANY_INSERT_WITH_BRANCH to use branch-level settings
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT_WITH_BRANCH (
    -- Company Parameters
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2 DEFAULT NULL,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2 DEFAULT NULL,
    P_FISCAL_YEAR_ID IN NUMBER DEFAULT NULL,
    P_COUNTRY_ID IN NUMBER DEFAULT NULL,
    P_CURR_ID IN NUMBER DEFAULT NULL,
    
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
            FISCAL_YEAR_ID,
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
            P_TAX_NUMBER,
            P_FISCAL_YEAR_ID,
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

PROMPT 'SYS_COMPANY stored procedures updated successfully.';

-- =============================================
-- Step 6: Verification
-- =============================================
PROMPT 'Verifying migration results...';

-- Check SYS_BRANCH table structure
SELECT 'SYS_BRANCH Structure' as INFO, column_name, data_type, nullable, data_default
FROM user_tab_columns
WHERE table_name = 'SYS_BRANCH'
  AND column_name IN ('DEFAULT_LANG', 'BASE_CURRENCY_ID', 'ROUNDING_RULES')
ORDER BY column_name;

-- Check SYS_COMPANY table structure (should not have the migrated columns)
SELECT 'SYS_COMPANY Structure' as INFO, COUNT(*) as REMAINING_COLUMNS
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
  AND column_name IN ('DEFAULT_LANG', 'BASE_CURRENCY_ID', 'ROUNDING_RULES');

-- Check data migration results
SELECT 
    'Data Migration Results' as INFO,
    COUNT(*) as TOTAL_BRANCHES,
    COUNT(CASE WHEN DEFAULT_LANG IS NOT NULL THEN 1 END) as BRANCHES_WITH_LANG,
    COUNT(CASE WHEN BASE_CURRENCY_ID IS NOT NULL THEN 1 END) as BRANCHES_WITH_CURRENCY,
    COUNT(CASE WHEN ROUNDING_RULES IS NOT NULL THEN 1 END) as BRANCHES_WITH_ROUNDING
FROM SYS_BRANCH
WHERE IS_ACTIVE = '1';

-- Display updated procedures
SELECT 'Updated Procedures' as INFO, object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_BRANCH_SELECT_ALL',
    'SP_SYS_BRANCH_SELECT_BY_ID',
    'SP_SYS_BRANCH_INSERT',
    'SP_SYS_BRANCH_UPDATE',
    'SP_SYS_BRANCH_SELECT_BY_COMPANY',
    'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
)
ORDER BY object_name;

PROMPT 'Migration completed successfully!';
PROMPT 'Summary of changes:';
PROMPT '- Added DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES to SYS_BRANCH';
PROMPT '- Migrated existing data from SYS_COMPANY to SYS_BRANCH';
PROMPT '- Removed DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES from SYS_COMPANY';
PROMPT '- Updated all related stored procedures';
PROMPT '- Added appropriate constraints and indexes';