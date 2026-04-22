-- =============================================
-- ThinkOnErp API - SYS_USERS Change Password Procedure
-- Description: Stored procedure for changing user password
-- =============================================

-- =============================================
-- Procedure: SP_SYS_USERS_CHANGE_PASSWORD
-- Description: Changes the password for a user
-- Parameters:
--   P_ROW_ID: The user ID
--   P_NEW_PASSWORD: The new SHA-256 hashed password
--   P_UPDATE_USER: User performing the password change
--   P_ROWS_AFFECTED: Output parameter returning number of rows affected
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_CHANGE_PASSWORD (
    P_ROW_ID IN NUMBER,
    P_NEW_PASSWORD IN NVARCHAR2,
    P_UPDATE_USER IN NVARCHAR2,
    P_ROWS_AFFECTED OUT NUMBER
)
AS
BEGIN
    -- Update the password
    UPDATE SYS_USERS
    SET 
        PASSWORD = P_NEW_PASSWORD,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID
    AND IS_ACTIVE = '1';
    
    -- Get number of rows affected
    P_ROWS_AFFECTED := SQL%ROWCOUNT;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        P_ROWS_AFFECTED := 0;
        RAISE_APPLICATION_ERROR(-20309, 'Error changing user password: ' || SQLERRM);
END SP_SYS_USERS_CHANGE_PASSWORD;
/

-- =============================================
-- Verification: Display created procedure
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name = 'SP_SYS_USERS_CHANGE_PASSWORD'
ORDER BY object_name;
