-- =============================================
-- ThinkOnErp API - SYS_COMPANY Stored Procedures
-- Description: CRUD stored procedures for SYS_COMPANY table
-- Requirements: 19.1, 19.2, 19.3, 19.4, 19.5, 19.6, 19.7, 19.8
-- =============================================

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_ALL
-- Description: Retrieves all active companies
-- Returns: SYS_REFCURSOR with all companies where IS_ACTIVE = '1'
-- Requirements: 19.1
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
        COUNTRY_ID,
        CURR_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_COMPANY
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving companies: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_BY_ID
-- Description: Retrieves a specific company by ID
-- Parameters:
--   P_ROW_ID: The company ID to retrieve
-- Returns: SYS_REFCURSOR with the matching company
-- Requirements: 19.2
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
        COUNTRY_ID,
        CURR_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_COMPANY
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving company by ID: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT
-- Description: Inserts a new company record
-- Parameters:
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_COUNTRY_ID: Country ID (optional)
--   P_CURR_ID: Currency ID (optional)
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new company ID
-- Requirements: 19.3, 19.4
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
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
        COUNTRY_ID,
        CURR_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
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
        RAISE_APPLICATION_ERROR(-20203, 'Error inserting company: ' || SQLERRM);
END SP_SYS_COMPANY_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_UPDATE
-- Description: Updates an existing company record
-- Parameters:
--   P_ROW_ID: The company ID to update
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_COUNTRY_ID: Country ID (optional)
--   P_CURR_ID: Currency ID (optional)
--   P_UPDATE_USER: User updating the record
-- Requirements: 19.5, 19.6
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the company record
    UPDATE SYS_COMPANY
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
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
-- Procedure: SP_SYS_COMPANY_DELETE
-- Description: Soft deletes a company by setting IS_ACTIVE to '0'
-- Parameters:
--   P_ROW_ID: The company ID to delete
-- Requirements: 19.7, 19.8
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_DELETE (
    P_ROW_ID IN NUMBER
)
AS
BEGIN
    -- Soft delete by setting IS_ACTIVE to '0'
    UPDATE SYS_COMPANY
    SET IS_ACTIVE = '0'
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20206, 'No company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20207, 'Error deleting company: ' || SQLERRM);
END SP_SYS_COMPANY_DELETE;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_SELECT_ALL',
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_COMPANY_INSERT',
    'SP_SYS_COMPANY_UPDATE',
    'SP_SYS_COMPANY_DELETE'
)
ORDER BY object_name;
