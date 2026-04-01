-- =============================================
-- ThinkOnErp API - SYS_ROLE Stored Procedures
-- Description: CRUD stored procedures for SYS_ROLE table
-- Requirements: 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7, 17.8, 28.1, 28.2, 28.3, 28.4, 28.5
-- =============================================

-- =============================================
-- Procedure: SP_SYS_ROLE_SELECT_ALL
-- Description: Retrieves all active roles
-- Returns: SYS_REFCURSOR with all roles where IS_ACTIVE = '1'
-- Requirements: 17.1
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_ROLE_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        NOTE,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_ROLE
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20001, 'Error retrieving roles: ' || SQLERRM);
END SP_SYS_ROLE_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_ROLE_SELECT_BY_ID
-- Description: Retrieves a specific role by ID
-- Parameters:
--   P_ROW_ID: The role ID to retrieve
-- Returns: SYS_REFCURSOR with the matching role
-- Requirements: 17.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_ROLE_SELECT_BY_ID (
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
        NOTE,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_ROLE
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20002, 'Error retrieving role by ID: ' || SQLERRM);
END SP_SYS_ROLE_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_ROLE_INSERT
-- Description: Inserts a new role record
-- Parameters:
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_NOTE: Optional note
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new role ID
-- Requirements: 17.3, 17.4, 28.1, 28.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_ROLE_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_NOTE IN VARCHAR2,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Generate new ID from sequence
    SELECT SEQ_SYS_ROLE.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new role record
    INSERT INTO SYS_ROLE (
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        NOTE,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_NOTE,
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20003, 'Error inserting role: ' || SQLERRM);
END SP_SYS_ROLE_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_ROLE_UPDATE
-- Description: Updates an existing role record
-- Parameters:
--   P_ROW_ID: The role ID to update
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_NOTE: Optional note
--   P_UPDATE_USER: User updating the record
-- Requirements: 17.5, 17.6, 28.3, 28.4, 28.5
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_ROLE_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_NOTE IN VARCHAR2,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the role record
    UPDATE SYS_ROLE
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        NOTE = P_NOTE,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20004, 'No role found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20005, 'Error updating role: ' || SQLERRM);
END SP_SYS_ROLE_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_ROLE_DELETE
-- Description: Soft deletes a role by setting IS_ACTIVE to '0'
-- Parameters:
--   P_ROW_ID: The role ID to delete
-- Requirements: 17.7, 17.8
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_ROLE_DELETE (
    P_ROW_ID IN NUMBER
)
AS
BEGIN
    -- Soft delete by setting IS_ACTIVE to '0'
    UPDATE SYS_ROLE
    SET IS_ACTIVE = '0'
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20006, 'No role found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20007, 'Error deleting role: ' || SQLERRM);
END SP_SYS_ROLE_DELETE;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_ROLE_SELECT_ALL',
    'SP_SYS_ROLE_SELECT_BY_ID',
    'SP_SYS_ROLE_INSERT',
    'SP_SYS_ROLE_UPDATE',
    'SP_SYS_ROLE_DELETE'
)
ORDER BY object_name;
