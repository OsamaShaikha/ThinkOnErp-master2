-- =====================================================
-- Super Admin Login - Stored Procedure
-- =====================================================
-- Description: Authenticates a super admin by username and password
-- Returns: Super admin record if credentials are valid and account is active
-- =====================================================

CREATE OR REPLACE PROCEDURE SP_SYS_SUPER_ADMIN_LOGIN(
    P_USER_NAME IN NVARCHAR2,
    P_PASSWORD IN NVARCHAR2,
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
    WHERE UPPER(USER_NAME) = UPPER(P_USER_NAME)
      AND PASSWORD = P_PASSWORD
      AND IS_ACTIVE = '1';
END SP_SYS_SUPER_ADMIN_LOGIN;
/

-- =====================================================
-- Add Refresh Token Support to SYS_SUPER_ADMIN Table
-- =====================================================

-- Check if columns exist before adding them
DECLARE
    v_column_exists NUMBER;
BEGIN
    -- Check for REFRESH_TOKEN column
    SELECT COUNT(*) INTO v_column_exists
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'SYS_SUPER_ADMIN'
    AND COLUMN_NAME = 'REFRESH_TOKEN';
    
    IF v_column_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_SUPER_ADMIN ADD REFRESH_TOKEN NVARCHAR2(500)';
        DBMS_OUTPUT.PUT_LINE('Added REFRESH_TOKEN column to SYS_SUPER_ADMIN');
    END IF;
    
    -- Check for REFRESH_TOKEN_EXPIRY column
    SELECT COUNT(*) INTO v_column_exists
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'SYS_SUPER_ADMIN'
    AND COLUMN_NAME = 'REFRESH_TOKEN_EXPIRY';
    
    IF v_column_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_SUPER_ADMIN ADD REFRESH_TOKEN_EXPIRY DATE';
        DBMS_OUTPUT.PUT_LINE('Added REFRESH_TOKEN_EXPIRY column to SYS_SUPER_ADMIN');
    END IF;
END;
/

COMMIT;

-- =====================================================
-- Script Execution Complete
-- =====================================================
-- Summary:
-- - Created SP_SYS_SUPER_ADMIN_LOGIN procedure
-- - Added REFRESH_TOKEN column to SYS_SUPER_ADMIN
-- - Added REFRESH_TOKEN_EXPIRY column to SYS_SUPER_ADMIN
-- =====================================================
