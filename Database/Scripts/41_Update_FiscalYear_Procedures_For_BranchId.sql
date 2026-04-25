-- =============================================
-- Script: 41_Update_FiscalYear_Procedures_For_BranchId.sql
-- Description: Update fiscal year stored procedures to include BRANCH_ID parameter
-- Author: System
-- Date: 2026-04-25
-- =============================================

PROMPT ========================================
PROMPT Updating Fiscal Year Stored Procedures
PROMPT ========================================

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_SELECT_ALL
-- Description: Retrieves all active fiscal years (updated to include BRANCH_ID)
-- Returns: SYS_REFCURSOR with all fiscal years where IS_ACTIVE = '1'
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
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
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_FISCAL_YEAR
    WHERE IS_ACTIVE = '1'
    ORDER BY COMPANY_ID, BRANCH_ID, START_DATE DESC;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20301, 'Error retrieving fiscal years: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_SELECT_BY_ID
-- Description: Retrieves a specific fiscal year by ID (updated to include BRANCH_ID)
-- Parameters:
--   P_ROW_ID: The fiscal year ID to retrieve
-- Returns: SYS_REFCURSOR with the matching fiscal year
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
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
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_FISCAL_YEAR
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20302, 'Error retrieving fiscal year by ID: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY
-- Description: Retrieves all fiscal years for a specific company (updated to include BRANCH_ID)
-- Parameters:
--   P_COMPANY_ID: The company ID to retrieve fiscal years for
-- Returns: SYS_REFCURSOR with matching fiscal years
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY (
    P_COMPANY_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
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
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_FISCAL_YEAR
    WHERE COMPANY_ID = P_COMPANY_ID
    AND IS_ACTIVE = '1'
    ORDER BY BRANCH_ID, START_DATE DESC;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20303, 'Error retrieving fiscal years by company: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH
-- Description: Retrieves all fiscal years for a specific branch
-- Parameters:
--   P_BRANCH_ID: The branch ID to retrieve fiscal years for
-- Returns: SYS_REFCURSOR with matching fiscal years
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH (
    P_BRANCH_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
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
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_FISCAL_YEAR
    WHERE BRANCH_ID = P_BRANCH_ID
    AND IS_ACTIVE = '1'
    ORDER BY START_DATE DESC;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20320, 'Error retrieving fiscal years by branch: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_INSERT
-- Description: Inserts a new fiscal year record (updated to include BRANCH_ID)
-- Parameters:
--   P_COMPANY_ID: Company ID (foreign key to SYS_COMPANY)
--   P_BRANCH_ID: Branch ID (foreign key to SYS_BRANCH)
--   P_FISCAL_YEAR_CODE: Fiscal year code (e.g., 'FY2024')
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_START_DATE: Fiscal year start date
--   P_END_DATE: Fiscal year end date
--   P_IS_CLOSED: Closed flag ('1' or '0')
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new fiscal year ID
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_INSERT (
    P_COMPANY_ID IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_FISCAL_YEAR_CODE IN VARCHAR2,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_IS_CLOSED IN CHAR,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Validate date range
    IF P_END_DATE <= P_START_DATE THEN
        RAISE_APPLICATION_ERROR(-20304, 'End date must be after start date');
    END IF;
    
    -- Validate that branch belongs to company
    DECLARE
        V_BRANCH_COMPANY_ID NUMBER;
    BEGIN
        SELECT PAR_ROW_ID INTO V_BRANCH_COMPANY_ID
        FROM SYS_BRANCH
        WHERE ROW_ID = P_BRANCH_ID;
        
        IF V_BRANCH_COMPANY_ID != P_COMPANY_ID THEN
            RAISE_APPLICATION_ERROR(-20321, 'Branch does not belong to the specified company');
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20322, 'Branch not found');
    END;
    
    -- Generate new ID from sequence
    SELECT SEQ_SYS_FISCAL_YEAR.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
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
        P_NEW_ID,
        P_COMPANY_ID,
        P_BRANCH_ID,
        P_FISCAL_YEAR_CODE,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_START_DATE,
        P_END_DATE,
        NVL(P_IS_CLOSED, '0'),
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20305, 'Fiscal year code already exists for this company');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20306, 'Error inserting fiscal year: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_UPDATE
-- Description: Updates an existing fiscal year record (updated to include BRANCH_ID)
-- Parameters:
--   P_ROW_ID: The fiscal year ID to update
--   P_COMPANY_ID: Company ID (foreign key to SYS_COMPANY)
--   P_BRANCH_ID: Branch ID (foreign key to SYS_BRANCH)
--   P_FISCAL_YEAR_CODE: Fiscal year code
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_START_DATE: Fiscal year start date
--   P_END_DATE: Fiscal year end date
--   P_IS_CLOSED: Closed flag ('1' or '0')
--   P_UPDATE_USER: User updating the record
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_UPDATE (
    P_ROW_ID IN NUMBER,
    P_COMPANY_ID IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_FISCAL_YEAR_CODE IN VARCHAR2,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_IS_CLOSED IN CHAR,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Validate date range
    IF P_END_DATE <= P_START_DATE THEN
        RAISE_APPLICATION_ERROR(-20307, 'End date must be after start date');
    END IF;
    
    -- Validate that branch belongs to company
    DECLARE
        V_BRANCH_COMPANY_ID NUMBER;
    BEGIN
        SELECT PAR_ROW_ID INTO V_BRANCH_COMPANY_ID
        FROM SYS_BRANCH
        WHERE ROW_ID = P_BRANCH_ID;
        
        IF V_BRANCH_COMPANY_ID != P_COMPANY_ID THEN
            RAISE_APPLICATION_ERROR(-20323, 'Branch does not belong to the specified company');
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20324, 'Branch not found');
    END;
    
    -- Update the fiscal year record
    UPDATE SYS_FISCAL_YEAR
    SET 
        COMPANY_ID = P_COMPANY_ID,
        BRANCH_ID = P_BRANCH_ID,
        FISCAL_YEAR_CODE = P_FISCAL_YEAR_CODE,
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        START_DATE = P_START_DATE,
        END_DATE = P_END_DATE,
        IS_CLOSED = P_IS_CLOSED,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20308, 'No fiscal year found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20309, 'Fiscal year code already exists for this company');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20310, 'Error updating fiscal year: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_UPDATE;
/

PROMPT ========================================
PROMPT Fiscal Year Procedures Updated Successfully
PROMPT ========================================

-- Verification: Display updated procedures
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_FISCAL_YEAR_SELECT_ALL',
    'SP_SYS_FISCAL_YEAR_SELECT_BY_ID',
    'SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY',
    'SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH',
    'SP_SYS_FISCAL_YEAR_INSERT',
    'SP_SYS_FISCAL_YEAR_UPDATE'
)
ORDER BY object_name;

PROMPT 
PROMPT Summary of changes:
PROMPT - Added BRANCH_ID to all SELECT procedures
PROMPT - Added P_BRANCH_ID parameter to INSERT procedure
PROMPT - Added P_BRANCH_ID parameter to UPDATE procedure
PROMPT - Added new SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH procedure
PROMPT - Added validation to ensure branch belongs to company
PROMPT 
PROMPT Next steps:
PROMPT 1. Run script 40_Add_BranchId_To_FiscalYear.sql if not already executed
PROMPT 2. Update application code to pass BRANCH_ID parameter
PROMPT 3. Test fiscal year creation and updates with branch association

