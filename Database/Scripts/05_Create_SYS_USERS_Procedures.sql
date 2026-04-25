-- =============================================
-- ThinkOnErp API - SYS_USERS Stored Procedures
-- Description: CRUD stored procedures for SYS_USERS table including authentication
-- Requirements: 21.1, 21.2, 21.3, 21.4, 21.5, 21.6, 21.7, 21.8, 21.9, 21.10, 21.11
-- =============================================

-- =============================================
-- Procedure: SP_SYS_USERS_SELECT_ALL
-- Description: Retrieves all active users
-- Returns: SYS_REFCURSOR with all users where IS_ACTIVE = '1'
-- Requirements: 21.1
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        PHONE,
        PHONE2,
        ROLE,
        BRANCH_ID,
        EMAIL,
        LAST_LOGIN_DATE,
        IS_ACTIVE,
        IS_ADMIN,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_USERS
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20301, 'Error retrieving users: ' || SQLERRM);
END SP_SYS_USERS_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_USERS_SELECT_BY_ID
-- Description: Retrieves a specific user by ID
-- Parameters:
--   P_ROW_ID: The user ID to retrieve
-- Returns: SYS_REFCURSOR with the matching user
-- Requirements: 21.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_SELECT_BY_ID (
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
        USER_NAME,
        PASSWORD,
        PHONE,
        PHONE2,
        ROLE,
        BRANCH_ID,
        EMAIL,
        LAST_LOGIN_DATE,
        IS_ACTIVE,
        IS_ADMIN,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_USERS
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20302, 'Error retrieving user by ID: ' || SQLERRM);
END SP_SYS_USERS_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_USERS_INSERT
-- Description: Inserts a new user record
-- Parameters:
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_USER_NAME: Unique username
--   P_PASSWORD: SHA-256 hashed password
--   P_PHONE: Phone number (optional)
--   P_PHONE2: Secondary phone number (optional)
--   P_ROLE: Role ID (foreign key to SYS_ROLE, optional)
--   P_BRANCH_ID: Branch ID (foreign key to SYS_BRANCH, optional)
--   P_EMAIL: Email address (optional)
--   P_IS_ADMIN: Admin flag ('1' or '0')
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new user ID
-- Requirements: 21.3, 21.4, 28.1, 28.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_USER_NAME IN VARCHAR2,
    P_PASSWORD IN VARCHAR2,
    P_PHONE IN VARCHAR2,
    P_PHONE2 IN VARCHAR2,
    P_ROLE IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_EMAIL IN VARCHAR2,
    P_IS_ADMIN IN CHAR,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Generate new ID from sequence
    SELECT SEQ_SYS_USERS.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new user record
    INSERT INTO SYS_USERS (
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        PHONE,
        PHONE2,
        ROLE,
        BRANCH_ID,
        EMAIL,
        IS_ACTIVE,
        IS_ADMIN,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_USER_NAME,
        P_PASSWORD,
        P_PHONE,
        P_PHONE2,
        P_ROLE,
        P_BRANCH_ID,
        P_EMAIL,
        '1',
        P_IS_ADMIN,
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20303, 'Error inserting user: ' || SQLERRM);
END SP_SYS_USERS_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_USERS_UPDATE
-- Description: Updates an existing user record
-- Parameters:
--   P_ROW_ID: The user ID to update
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_USER_NAME: Unique username
--   P_PASSWORD: SHA-256 hashed password
--   P_PHONE: Phone number (optional)
--   P_PHONE2: Secondary phone number (optional)
--   P_ROLE: Role ID (foreign key to SYS_ROLE, optional)
--   P_BRANCH_ID: Branch ID (foreign key to SYS_BRANCH, optional)
--   P_EMAIL: Email address (optional)
--   P_IS_ADMIN: Admin flag ('1' or '0')
--   P_UPDATE_USER: User updating the record
-- Requirements: 21.5, 21.6, 28.3, 28.4, 28.5
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_USER_NAME IN VARCHAR2,
    P_PASSWORD IN VARCHAR2,
    P_PHONE IN VARCHAR2,
    P_PHONE2 IN VARCHAR2,
    P_ROLE IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_EMAIL IN VARCHAR2,
    P_IS_ADMIN IN CHAR,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the user record
    UPDATE SYS_USERS
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        USER_NAME = P_USER_NAME,
        PASSWORD = P_PASSWORD,
        PHONE = P_PHONE,
        PHONE2 = P_PHONE2,
        ROLE = P_ROLE,
        BRANCH_ID = P_BRANCH_ID,
        EMAIL = P_EMAIL,
        IS_ADMIN = P_IS_ADMIN,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20304, 'No user found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20305, 'Error updating user: ' || SQLERRM);
END SP_SYS_USERS_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_USERS_DELETE
-- Description: Soft deletes a user by setting IS_ACTIVE to '0'
-- Parameters:
--   P_ROW_ID: The user ID to delete
-- Requirements: 21.7
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_DELETE (
    P_ROW_ID IN NUMBER
)
AS
BEGIN
    -- Soft delete by setting IS_ACTIVE to '0'
    UPDATE SYS_USERS
    SET IS_ACTIVE = '0'
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20306, 'No user found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20307, 'Error deleting user: ' || SQLERRM);
END SP_SYS_USERS_DELETE;
/

-- =============================================
-- Procedure: SP_SYS_USERS_LOGIN
-- Description: Authenticates a user by username and password
-- Parameters:
--   P_USER_NAME: The username to authenticate
--   P_PASSWORD: The SHA-256 hashed password to verify
-- Returns: SYS_REFCURSOR with user record if credentials match and IS_ACTIVE = '1', empty cursor otherwise
-- Requirements: 21.8, 21.9, 21.10, 21.11
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_LOGIN (
    P_USER_NAME IN VARCHAR2,
    P_PASSWORD IN VARCHAR2,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        PHONE,
        PHONE2,
        ROLE,
        BRANCH_ID,
        EMAIL,
        LAST_LOGIN_DATE,
        IS_ACTIVE,
        IS_ADMIN,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_USERS
    WHERE USER_NAME = P_USER_NAME
      AND PASSWORD = P_PASSWORD
      AND IS_ACTIVE = '1';
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20308, 'Error during user authentication: ' || SQLERRM);
END SP_SYS_USERS_LOGIN;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_USERS_SELECT_ALL',
    'SP_SYS_USERS_SELECT_BY_ID',
    'SP_SYS_USERS_INSERT',
    'SP_SYS_USERS_UPDATE',
    'SP_SYS_USERS_DELETE',
    'SP_SYS_USERS_LOGIN'
)
ORDER BY object_name;
-- =============================================
-- Procedure: SP_SYS_USERS_SELECT_ADMINS
-- Description: Retrieves all active admin users
-- Returns: SYS_REFCURSOR with all admin users where IS_ACTIVE = 'Y' and IS_ADMIN = 'Y'
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_SELECT_ADMINS (
    P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        PHONE,
        EMAIL,
        IS_ADMIN,
        ROLE,
        BRANCH_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        REFRESH_TOKEN,
        REFRESH_TOKEN_EXPIRY,
        FORCE_LOGOUT_DATE
    FROM SYS_USERS
    WHERE IS_ACTIVE = 'Y' 
      AND IS_ADMIN = 'Y'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20309, 'Error retrieving admin users: ' || SQLERRM);
END SP_SYS_USERS_SELECT_ADMINS;
/