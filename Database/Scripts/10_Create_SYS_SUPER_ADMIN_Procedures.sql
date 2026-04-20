-- =====================================================
-- Super Admin Management - Stored Procedures
-- =====================================================

-- =====================================================
-- 1. SP_SYS_SUPER_ADMIN_SELECT_ALL
-- Retrieves all active super admin accounts
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_SELECT_ALL(
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        EMAIL,
        PHONE,
        TWO_FA_SECRET,
        CASE WHEN TWO_FA_ENABLED = '1' THEN 1 ELSE 0 END AS TWO_FA_ENABLED,
        CASE WHEN IS_ACTIVE = '1' THEN 1 ELSE 0 END AS IS_ACTIVE,
        LAST_LOGIN_DATE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_SUPER_ADMIN
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_DESC_E;
END;
/

-- =====================================================
-- 2. SP_SYS_SUPER_ADMIN_SELECT_BY_ID
-- Retrieves a super admin by ID
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_SELECT_BY_ID(
    p_row_id IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        EMAIL,
        PHONE,
        TWO_FA_SECRET,
        CASE WHEN TWO_FA_ENABLED = '1' THEN 1 ELSE 0 END AS TWO_FA_ENABLED,
        CASE WHEN IS_ACTIVE = '1' THEN 1 ELSE 0 END AS IS_ACTIVE,
        LAST_LOGIN_DATE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_SUPER_ADMIN
    WHERE ROW_ID = p_row_id;
END;
/

-- =====================================================
-- 3. SP_SYS_SUPER_ADMIN_SELECT_BY_USERNAME
-- Retrieves a super admin by username
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_SELECT_BY_USERNAME(
    p_username IN NVARCHAR2,
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        EMAIL,
        PHONE,
        TWO_FA_SECRET,
        CASE WHEN TWO_FA_ENABLED = '1' THEN 1 ELSE 0 END AS TWO_FA_ENABLED,
        CASE WHEN IS_ACTIVE = '1' THEN 1 ELSE 0 END AS IS_ACTIVE,
        LAST_LOGIN_DATE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_SUPER_ADMIN
    WHERE UPPER(USER_NAME) = UPPER(p_username);
END;
/

-- =====================================================
-- 4. SP_SYS_SUPER_ADMIN_SELECT_BY_EMAIL
-- Retrieves a super admin by email
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_SELECT_BY_EMAIL(
    p_email IN NVARCHAR2,
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        EMAIL,
        PHONE,
        TWO_FA_SECRET,
        CASE WHEN TWO_FA_ENABLED = '1' THEN 1 ELSE 0 END AS TWO_FA_ENABLED,
        CASE WHEN IS_ACTIVE = '1' THEN 1 ELSE 0 END AS IS_ACTIVE,
        LAST_LOGIN_DATE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_SUPER_ADMIN
    WHERE UPPER(EMAIL) = UPPER(p_email);
END;
/

-- =====================================================
-- 5. SP_SYS_SUPER_ADMIN_INSERT
-- Creates a new super admin account
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_INSERT(
    p_row_desc IN NVARCHAR2,
    p_row_desc_e IN NVARCHAR2,
    p_user_name IN NVARCHAR2,
    p_password IN NVARCHAR2,
    p_email IN NVARCHAR2,
    p_phone IN NVARCHAR2,
    p_creation_user IN NVARCHAR2,
    p_new_id OUT NUMBER
)
AS
BEGIN
    SELECT SEQ_SYS_SUPER_ADMIN.NEXTVAL INTO p_new_id FROM DUAL;
    
    INSERT INTO SYS_SUPER_ADMIN (
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        USER_NAME,
        PASSWORD,
        EMAIL,
        PHONE,
        TWO_FA_ENABLED,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        p_new_id,
        p_row_desc,
        p_row_desc_e,
        p_user_name,
        p_password,
        p_email,
        p_phone,
        '0',
        '1',
        p_creation_user,
        SYSDATE
    );
    
    COMMIT;
END;
/

-- =====================================================
-- 6. SP_SYS_SUPER_ADMIN_UPDATE
-- Updates an existing super admin account
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_UPDATE(
    p_row_id IN NUMBER,
    p_row_desc IN NVARCHAR2,
    p_row_desc_e IN NVARCHAR2,
    p_email IN NVARCHAR2,
    p_phone IN NVARCHAR2,
    p_update_user IN NVARCHAR2,
    p_rows_affected OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_SUPER_ADMIN
    SET 
        ROW_DESC = p_row_desc,
        ROW_DESC_E = p_row_desc_e,
        EMAIL = p_email,
        PHONE = p_phone,
        UPDATE_USER = p_update_user,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = p_row_id
    AND IS_ACTIVE = '1';
    
    p_rows_affected := SQL%ROWCOUNT;
    COMMIT;
END;
/

-- =====================================================
-- 7. SP_SYS_SUPER_ADMIN_DELETE
-- Soft deletes a super admin account
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_DELETE(
    p_row_id IN NUMBER,
    p_rows_affected OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_SUPER_ADMIN
    SET IS_ACTIVE = '0',
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = p_row_id;
    
    p_rows_affected := SQL%ROWCOUNT;
    COMMIT;
END;
/

-- =====================================================
-- 8. SP_SYS_SUPER_ADMIN_CHANGE_PASSWORD
-- Changes the password for a super admin
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_CHANGE_PASSWORD(
    p_row_id IN NUMBER,
    p_new_password IN NVARCHAR2,
    p_update_user IN NVARCHAR2,
    p_rows_affected OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_SUPER_ADMIN
    SET 
        PASSWORD = p_new_password,
        UPDATE_USER = p_update_user,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = p_row_id
    AND IS_ACTIVE = '1';
    
    p_rows_affected := SQL%ROWCOUNT;
    COMMIT;
END;
/

-- =====================================================
-- 9. SP_SYS_SUPER_ADMIN_ENABLE_2FA
-- Enables 2FA for a super admin
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_ENABLE_2FA(
    p_row_id IN NUMBER,
    p_two_fa_secret IN NVARCHAR2,
    p_update_user IN NVARCHAR2,
    p_rows_affected OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_SUPER_ADMIN
    SET 
        TWO_FA_SECRET = p_two_fa_secret,
        TWO_FA_ENABLED = '1',
        UPDATE_USER = p_update_user,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = p_row_id
    AND IS_ACTIVE = '1';
    
    p_rows_affected := SQL%ROWCOUNT;
    COMMIT;
END;
/

-- =====================================================
-- 10. SP_SYS_SUPER_ADMIN_DISABLE_2FA
-- Disables 2FA for a super admin
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_DISABLE_2FA(
    p_row_id IN NUMBER,
    p_update_user IN NVARCHAR2,
    p_rows_affected OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_SUPER_ADMIN
    SET 
        TWO_FA_SECRET = NULL,
        TWO_FA_ENABLED = '0',
        UPDATE_USER = p_update_user,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = p_row_id
    AND IS_ACTIVE = '1';
    
    p_rows_affected := SQL%ROWCOUNT;
    COMMIT;
END;
/

-- =====================================================
-- 11. SP_SYS_SUPER_ADMIN_UPDATE_LAST_LOGIN
-- Updates the last login date for a super admin
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_UPDATE_LAST_LOGIN(
    p_row_id IN NUMBER,
    p_rows_affected OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_SUPER_ADMIN
    SET LAST_LOGIN_DATE = SYSDATE
    WHERE ROW_ID = p_row_id
    AND IS_ACTIVE = '1';
    
    p_rows_affected := SQL%ROWCOUNT;
    COMMIT;
END;
/

COMMIT;

-- =====================================================
-- Script Execution Complete
-- =====================================================
