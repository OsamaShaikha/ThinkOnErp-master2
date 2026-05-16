-- =============================================
-- Script: 89_Create_Branch_Permission_Procedures.sql
-- Description: Creates stored procedures for branch-level permissions (systems and screens)
-- Author: ThinkOnERP Development Team
-- Date: 2024
-- =============================================

-- =============================================
-- SYS_BRANCH_SYSTEM Procedures
-- =============================================

-- Select all branch systems by branch ID
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SYSTEM_SELECT_BY_BRANCH (
    P_BRANCH_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        bs.ROW_ID,
        bs.BRANCH_ID,
        bs.SYSTEM_ID,
        s.SYSTEM_CODE,
        s.SYSTEM_NAME,
        s.SYSTEM_NAME_E,
        bs.IS_ALLOWED,
        bs.GRANTED_BY,
        sa.USER_NAME AS GRANTED_BY_NAME,
        bs.GRANTED_DATE,
        bs.REVOKED_DATE,
        bs.NOTES,
        bs.CREATION_USER,
        bs.CREATION_DATE,
        bs.UPDATE_USER,
        bs.UPDATE_DATE
    FROM SYS_BRANCH_SYSTEM bs
    INNER JOIN SYS_SYSTEM s ON bs.SYSTEM_ID = s.ROW_ID
    LEFT JOIN SYS_SUPER_ADMIN sa ON bs.GRANTED_BY = sa.ROW_ID
    WHERE bs.BRANCH_ID = P_BRANCH_ID
    ORDER BY s.SYSTEM_ORDER, s.SYSTEM_NAME;
END;
/

-- Select specific branch system
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SYSTEM_SELECT_BY_ID (
    P_BRANCH_ID IN NUMBER,
    P_SYSTEM_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        bs.ROW_ID,
        bs.BRANCH_ID,
        bs.SYSTEM_ID,
        s.SYSTEM_CODE,
        s.SYSTEM_NAME,
        s.SYSTEM_NAME_E,
        bs.IS_ALLOWED,
        bs.GRANTED_BY,
        sa.USER_NAME AS GRANTED_BY_NAME,
        bs.GRANTED_DATE,
        bs.REVOKED_DATE,
        bs.NOTES,
        bs.CREATION_USER,
        bs.CREATION_DATE,
        bs.UPDATE_USER,
        bs.UPDATE_DATE
    FROM SYS_BRANCH_SYSTEM bs
    INNER JOIN SYS_SYSTEM s ON bs.SYSTEM_ID = s.ROW_ID
    LEFT JOIN SYS_SUPER_ADMIN sa ON bs.GRANTED_BY = sa.ROW_ID
    WHERE bs.BRANCH_ID = P_BRANCH_ID
      AND bs.SYSTEM_ID = P_SYSTEM_ID;
END;
/

-- Grant system access to branch
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SYSTEM_GRANT (
    P_BRANCH_ID IN NUMBER,
    P_SYSTEM_ID IN NUMBER,
    P_GRANTED_BY IN NUMBER,
    P_NOTES IN VARCHAR2,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
) AS
    V_EXISTS NUMBER;
BEGIN
    -- Check if record already exists
    SELECT COUNT(*) INTO V_EXISTS
    FROM SYS_BRANCH_SYSTEM
    WHERE BRANCH_ID = P_BRANCH_ID
      AND SYSTEM_ID = P_SYSTEM_ID;

    IF V_EXISTS > 0 THEN
        -- Update existing record
        UPDATE SYS_BRANCH_SYSTEM
        SET IS_ALLOWED = '1',
            GRANTED_BY = P_GRANTED_BY,
            GRANTED_DATE = SYSDATE,
            REVOKED_DATE = NULL,
            NOTES = P_NOTES,
            UPDATE_USER = P_CREATION_USER,
            UPDATE_DATE = SYSDATE
        WHERE BRANCH_ID = P_BRANCH_ID
          AND SYSTEM_ID = P_SYSTEM_ID
        RETURNING ROW_ID INTO P_NEW_ID;
    ELSE
        -- Insert new record
        INSERT INTO SYS_BRANCH_SYSTEM (
            ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED,
            GRANTED_BY, GRANTED_DATE, NOTES,
            CREATION_USER, CREATION_DATE
        ) VALUES (
            SEQ_SYS_BRANCH_SYSTEM.NEXTVAL,
            P_BRANCH_ID,
            P_SYSTEM_ID,
            '1',
            P_GRANTED_BY,
            SYSDATE,
            P_NOTES,
            P_CREATION_USER,
            SYSDATE
        ) RETURNING ROW_ID INTO P_NEW_ID;
    END IF;

    COMMIT;
END;
/

-- Revoke system access from branch
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SYSTEM_REVOKE (
    P_BRANCH_ID IN NUMBER,
    P_SYSTEM_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
) AS
BEGIN
    UPDATE SYS_BRANCH_SYSTEM
    SET IS_ALLOWED = '0',
        REVOKED_DATE = SYSDATE,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE BRANCH_ID = P_BRANCH_ID
      AND SYSTEM_ID = P_SYSTEM_ID;

    COMMIT;
END;
/

-- Check if branch system is allowed
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SYSTEM_IS_ALLOWED (
    P_BRANCH_ID IN NUMBER,
    P_SYSTEM_ID IN NUMBER,
    P_IS_ALLOWED OUT NUMBER
) AS
BEGIN
    SELECT CASE WHEN IS_ALLOWED = '1' THEN 1 ELSE 0 END
    INTO P_IS_ALLOWED
    FROM SYS_BRANCH_SYSTEM
    WHERE BRANCH_ID = P_BRANCH_ID
      AND SYSTEM_ID = P_SYSTEM_ID;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        -- If no record exists, default to allowed (backward compatible)
        P_IS_ALLOWED := 1;
END;
/

-- =============================================
-- SYS_BRANCH_SCREEN Procedures
-- =============================================

-- Select all branch screens by branch ID
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SCREEN_SELECT_BY_BRANCH (
    P_BRANCH_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        bsc.ROW_ID,
        bsc.BRANCH_ID,
        bsc.SCREEN_ID,
        sc.SCREEN_CODE,
        sc.SCREEN_NAME,
        sc.SCREEN_NAME_E,
        sc.SYSTEM_ID,
        s.SYSTEM_NAME,
        bsc.IS_ALLOWED,
        bsc.GRANTED_BY,
        sa.USER_NAME AS GRANTED_BY_NAME,
        bsc.GRANTED_DATE,
        bsc.REVOKED_DATE,
        bsc.NOTES,
        bsc.CREATION_USER,
        bsc.CREATION_DATE,
        bsc.UPDATE_USER,
        bsc.UPDATE_DATE
    FROM SYS_BRANCH_SCREEN bsc
    INNER JOIN SYS_SCREEN sc ON bsc.SCREEN_ID = sc.ROW_ID
    LEFT JOIN SYS_SYSTEM s ON sc.SYSTEM_ID = s.ROW_ID
    LEFT JOIN SYS_SUPER_ADMIN sa ON bsc.GRANTED_BY = sa.ROW_ID
    WHERE bsc.BRANCH_ID = P_BRANCH_ID
    ORDER BY s.SYSTEM_ORDER, sc.SCREEN_ORDER, sc.SCREEN_NAME;
END;
/

-- Select specific branch screen
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SCREEN_SELECT_BY_ID (
    P_BRANCH_ID IN NUMBER,
    P_SCREEN_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        bsc.ROW_ID,
        bsc.BRANCH_ID,
        bsc.SCREEN_ID,
        sc.SCREEN_CODE,
        sc.SCREEN_NAME,
        sc.SCREEN_NAME_E,
        sc.SYSTEM_ID,
        s.SYSTEM_NAME,
        bsc.IS_ALLOWED,
        bsc.GRANTED_BY,
        sa.USER_NAME AS GRANTED_BY_NAME,
        bsc.GRANTED_DATE,
        bsc.REVOKED_DATE,
        bsc.NOTES,
        bsc.CREATION_USER,
        bsc.CREATION_DATE,
        bsc.UPDATE_USER,
        bsc.UPDATE_DATE
    FROM SYS_BRANCH_SCREEN bsc
    INNER JOIN SYS_SCREEN sc ON bsc.SCREEN_ID = sc.ROW_ID
    LEFT JOIN SYS_SYSTEM s ON sc.SYSTEM_ID = s.ROW_ID
    LEFT JOIN SYS_SUPER_ADMIN sa ON bsc.GRANTED_BY = sa.ROW_ID
    WHERE bsc.BRANCH_ID = P_BRANCH_ID
      AND bsc.SCREEN_ID = P_SCREEN_ID;
END;
/

-- Grant screen access to branch
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SCREEN_GRANT (
    P_BRANCH_ID IN NUMBER,
    P_SCREEN_ID IN NUMBER,
    P_GRANTED_BY IN NUMBER,
    P_NOTES IN VARCHAR2,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
) AS
    V_EXISTS NUMBER;
BEGIN
    -- Check if record already exists
    SELECT COUNT(*) INTO V_EXISTS
    FROM SYS_BRANCH_SCREEN
    WHERE BRANCH_ID = P_BRANCH_ID
      AND SCREEN_ID = P_SCREEN_ID;

    IF V_EXISTS > 0 THEN
        -- Update existing record
        UPDATE SYS_BRANCH_SCREEN
        SET IS_ALLOWED = '1',
            GRANTED_BY = P_GRANTED_BY,
            GRANTED_DATE = SYSDATE,
            REVOKED_DATE = NULL,
            NOTES = P_NOTES,
            UPDATE_USER = P_CREATION_USER,
            UPDATE_DATE = SYSDATE
        WHERE BRANCH_ID = P_BRANCH_ID
          AND SCREEN_ID = P_SCREEN_ID
        RETURNING ROW_ID INTO P_NEW_ID;
    ELSE
        -- Insert new record
        INSERT INTO SYS_BRANCH_SCREEN (
            ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED,
            GRANTED_BY, GRANTED_DATE, NOTES,
            CREATION_USER, CREATION_DATE
        ) VALUES (
            SEQ_SYS_BRANCH_SCREEN.NEXTVAL,
            P_BRANCH_ID,
            P_SCREEN_ID,
            '1',
            P_GRANTED_BY,
            SYSDATE,
            P_NOTES,
            P_CREATION_USER,
            SYSDATE
        ) RETURNING ROW_ID INTO P_NEW_ID;
    END IF;

    COMMIT;
END;
/

-- Revoke screen access from branch
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SCREEN_REVOKE (
    P_BRANCH_ID IN NUMBER,
    P_SCREEN_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
) AS
BEGIN
    UPDATE SYS_BRANCH_SCREEN
    SET IS_ALLOWED = '0',
        REVOKED_DATE = SYSDATE,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE BRANCH_ID = P_BRANCH_ID
      AND SCREEN_ID = P_SCREEN_ID;

    COMMIT;
END;
/

-- Check if branch screen is allowed
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SCREEN_IS_ALLOWED (
    P_BRANCH_ID IN NUMBER,
    P_SCREEN_ID IN NUMBER,
    P_IS_ALLOWED OUT NUMBER
) AS
BEGIN
    SELECT CASE WHEN IS_ALLOWED = '1' THEN 1 ELSE 0 END
    INTO P_IS_ALLOWED
    FROM SYS_BRANCH_SCREEN
    WHERE BRANCH_ID = P_BRANCH_ID
      AND SCREEN_ID = P_SCREEN_ID;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        -- If no record exists, default to allowed (backward compatible)
        P_IS_ALLOWED := 1;
END;
/

-- =============================================
-- Helper Procedures
-- =============================================

-- Get all systems
CREATE OR REPLACE PROCEDURE SP_SYS_SYSTEM_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        SYSTEM_CODE,
        SYSTEM_NAME,
        SYSTEM_NAME_E,
        SYSTEM_ICON,
        SYSTEM_ORDER,
        IS_ACTIVE
    FROM SYS_SYSTEM
    WHERE IS_ACTIVE = '1'
    ORDER BY SYSTEM_ORDER, SYSTEM_NAME;
END;
/

-- Get all screens
CREATE OR REPLACE PROCEDURE SP_SYS_SCREEN_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        sc.ROW_ID,
        sc.SYSTEM_ID,
        s.SYSTEM_NAME,
        sc.SCREEN_CODE,
        sc.SCREEN_NAME,
        sc.SCREEN_NAME_E,
        sc.SCREEN_URL,
        sc.SCREEN_ORDER,
        sc.IS_ACTIVE
    FROM SYS_SCREEN sc
    LEFT JOIN SYS_SYSTEM s ON sc.SYSTEM_ID = s.ROW_ID
    WHERE sc.IS_ACTIVE = '1'
    ORDER BY s.SYSTEM_ORDER, sc.SCREEN_ORDER, sc.SCREEN_NAME;
END;
/

-- Get screens by system
CREATE OR REPLACE PROCEDURE SP_SYS_SCREEN_SELECT_BY_SYSTEM (
    P_SYSTEM_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        sc.ROW_ID,
        sc.SYSTEM_ID,
        s.SYSTEM_NAME,
        sc.SCREEN_CODE,
        sc.SCREEN_NAME,
        sc.SCREEN_NAME_E,
        sc.SCREEN_URL,
        sc.SCREEN_ORDER,
        sc.IS_ACTIVE
    FROM SYS_SCREEN sc
    LEFT JOIN SYS_SYSTEM s ON sc.SYSTEM_ID = s.ROW_ID
    WHERE sc.SYSTEM_ID = P_SYSTEM_ID
      AND sc.IS_ACTIVE = '1'
    ORDER BY sc.SCREEN_ORDER, sc.SCREEN_NAME;
END;
/

-- =============================================
-- Verification
-- =============================================
SELECT 'Branch permission procedures created successfully' AS STATUS FROM DUAL;
