-- =============================================
-- ThinkOnErp API - SYS_BRANCH Stored Procedures
-- Description: CRUD stored procedures for SYS_BRANCH table
-- Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8
-- =============================================

-- =============================================
-- Procedure: SP_SYS_BRANCH_SELECT_ALL
-- Description: Retrieves all active branches
-- Returns: SYS_REFCURSOR with all branches where IS_ACTIVE = '1'
-- Requirements: 20.1
-- =============================================
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
        UPDATE_DATE
    FROM SYS_BRANCH
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving branches: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_BRANCH_SELECT_BY_ID
-- Description: Retrieves a specific branch by ID
-- Parameters:
--   P_ROW_ID: The branch ID to retrieve
-- Returns: SYS_REFCURSOR with the matching branch
-- Requirements: 20.2
-- =============================================
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
        UPDATE_DATE
    FROM SYS_BRANCH
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving branch by ID: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_BRANCH_INSERT
-- Description: Inserts a new branch record
-- Parameters:
--   P_PAR_ROW_ID: Parent company ID (foreign key to SYS_COMPANY)
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_PHONE: Phone number (optional)
--   P_MOBILE: Mobile number (optional)
--   P_FAX: Fax number (optional)
--   P_EMAIL: Email address (optional)
--   P_IS_HEAD_BRANCH: Head branch flag ('1' or '0')
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new branch ID
-- Requirements: 20.3, 20.4, 28.1, 28.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_INSERT (
    P_PAR_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_PHONE IN VARCHAR2,
    P_MOBILE IN VARCHAR2,
    P_FAX IN VARCHAR2,
    P_EMAIL IN VARCHAR2,
    P_IS_HEAD_BRANCH IN CHAR,
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

-- =============================================
-- Procedure: SP_SYS_BRANCH_UPDATE
-- Description: Updates an existing branch record
-- Parameters:
--   P_ROW_ID: The branch ID to update
--   P_PAR_ROW_ID: Parent company ID (foreign key to SYS_COMPANY)
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_PHONE: Phone number (optional)
--   P_MOBILE: Mobile number (optional)
--   P_FAX: Fax number (optional)
--   P_EMAIL: Email address (optional)
--   P_IS_HEAD_BRANCH: Head branch flag ('1' or '0')
--   P_UPDATE_USER: User updating the record
-- Requirements: 20.5, 20.6, 28.3, 28.4, 28.5
-- =============================================
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

-- =============================================
-- Procedure: SP_SYS_BRANCH_DELETE
-- Description: Soft deletes a branch by setting IS_ACTIVE to '0'
-- Parameters:
--   P_ROW_ID: The branch ID to delete
-- Requirements: 20.7, 20.8
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_DELETE (
    P_ROW_ID IN NUMBER
)
AS
BEGIN
    -- Soft delete by setting IS_ACTIVE to '0'
    UPDATE SYS_BRANCH
    SET IS_ACTIVE = '0'
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20206, 'No branch found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20207, 'Error deleting branch: ' || SQLERRM);
END SP_SYS_BRANCH_DELETE;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_BRANCH_SELECT_ALL',
    'SP_SYS_BRANCH_SELECT_BY_ID',
    'SP_SYS_BRANCH_INSERT',
    'SP_SYS_BRANCH_UPDATE',
    'SP_SYS_BRANCH_DELETE'
)
ORDER BY object_name;
