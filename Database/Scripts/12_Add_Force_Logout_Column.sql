-- Add force logout column to SYS_USERS table
-- This allows super admins to force logout users by invalidating their tokens

-- Add force logout date column
ALTER TABLE SYS_USERS ADD (
    FORCE_LOGOUT_DATE DATE
);

-- Add comment to column
COMMENT ON COLUMN SYS_USERS.FORCE_LOGOUT_DATE IS 'Date when user was force logged out. Tokens issued before this date are invalid.';

-- Create stored procedure to force logout a user
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_FORCE_LOGOUT (
    P_USER_ID IN NUMBER,
    P_ADMIN_USER IN VARCHAR2,
    P_ROWS_AFFECTED OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_USERS
    SET FORCE_LOGOUT_DATE = SYSDATE,
        REFRESH_TOKEN = NULL,
        REFRESH_TOKEN_EXPIRY = NULL,
        UPDATE_USER = P_ADMIN_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_USER_ID
      AND IS_ACTIVE = '1';
    
    P_ROWS_AFFECTED := SQL%ROWCOUNT;
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE;
END SP_SYS_USERS_FORCE_LOGOUT;
/
