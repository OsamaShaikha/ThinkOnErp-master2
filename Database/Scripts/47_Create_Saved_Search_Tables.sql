-- =============================================
-- Advanced Search Enhancement - Saved Search Tables
-- Description: Tables and procedures for saved search functionality
-- Requirements: 8.6, 8.11, 19.9
-- Task: 10.1 - Create advanced search functionality
-- =============================================

-- =============================================
-- Table: SYS_SAVED_SEARCH
-- Description: Stores user-defined saved searches for quick access
-- =============================================
CREATE TABLE SYS_SAVED_SEARCH (
    ROW_ID NUMBER(19) PRIMARY KEY,
    USER_ID NUMBER(19) NOT NULL,
    SEARCH_NAME NVARCHAR2(100) NOT NULL,
    SEARCH_DESCRIPTION NVARCHAR2(500),
    SEARCH_CRITERIA NCLOB NOT NULL, -- JSON format storing all search parameters
    IS_PUBLIC CHAR(1) DEFAULT 'N' NOT NULL, -- Y = shared with all users, N = private
    IS_DEFAULT CHAR(1) DEFAULT 'N' NOT NULL, -- Y = default search for user
    USAGE_COUNT NUMBER(10) DEFAULT 0 NOT NULL,
    LAST_USED_DATE DATE,
    IS_ACTIVE CHAR(1) DEFAULT 'Y' NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL,
    UPDATE_USER NVARCHAR2(100),
    UPDATE_DATE DATE,
    
    CONSTRAINT FK_SAVED_SEARCH_USER FOREIGN KEY (USER_ID) REFERENCES SYS_USERS(ROW_ID),
    CONSTRAINT CHK_SAVED_SEARCH_PUBLIC CHECK (IS_PUBLIC IN ('Y', 'N')),
    CONSTRAINT CHK_SAVED_SEARCH_DEFAULT CHECK (IS_DEFAULT IN ('Y', 'N')),
    CONSTRAINT CHK_SAVED_SEARCH_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N'))
);

-- Create sequence for saved search
CREATE SEQUENCE SEQ_SYS_SAVED_SEARCH START WITH 1 INCREMENT BY 1;

-- Create indexes for performance
CREATE INDEX IDX_SAVED_SEARCH_USER ON SYS_SAVED_SEARCH(USER_ID, IS_ACTIVE);
CREATE INDEX IDX_SAVED_SEARCH_PUBLIC ON SYS_SAVED_SEARCH(IS_PUBLIC, IS_ACTIVE);
CREATE INDEX IDX_SAVED_SEARCH_NAME ON SYS_SAVED_SEARCH(SEARCH_NAME);

-- =============================================
-- Procedure: SP_SYS_SAVED_SEARCH_INSERT
-- Description: Creates a new saved search
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_SAVED_SEARCH_INSERT (
    P_USER_ID IN NUMBER,
    P_SEARCH_NAME IN NVARCHAR2,
    P_SEARCH_DESCRIPTION IN NVARCHAR2,
    P_SEARCH_CRITERIA IN NCLOB,
    P_IS_PUBLIC IN CHAR,
    P_IS_DEFAULT IN CHAR,
    P_CREATION_USER IN NVARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Generate new ID
    SELECT SEQ_SYS_SAVED_SEARCH.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- If this is set as default, unset other defaults for this user
    IF P_IS_DEFAULT = 'Y' THEN
        UPDATE SYS_SAVED_SEARCH
        SET IS_DEFAULT = 'N',
            UPDATE_USER = P_CREATION_USER,
            UPDATE_DATE = SYSDATE
        WHERE USER_ID = P_USER_ID AND IS_DEFAULT = 'Y';
    END IF;
    
    -- Insert new saved search
    INSERT INTO SYS_SAVED_SEARCH (
        ROW_ID,
        USER_ID,
        SEARCH_NAME,
        SEARCH_DESCRIPTION,
        SEARCH_CRITERIA,
        IS_PUBLIC,
        IS_DEFAULT,
        USAGE_COUNT,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_USER_ID,
        P_SEARCH_NAME,
        P_SEARCH_DESCRIPTION,
        P_SEARCH_CRITERIA,
        P_IS_PUBLIC,
        P_IS_DEFAULT,
        0,
        'Y',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20450, 'Error creating saved search: ' || SQLERRM);
END SP_SYS_SAVED_SEARCH_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_SAVED_SEARCH_UPDATE
-- Description: Updates an existing saved search
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_SAVED_SEARCH_UPDATE (
    P_ROW_ID IN NUMBER,
    P_SEARCH_NAME IN NVARCHAR2,
    P_SEARCH_DESCRIPTION IN NVARCHAR2,
    P_SEARCH_CRITERIA IN NCLOB,
    P_IS_PUBLIC IN CHAR,
    P_IS_DEFAULT IN CHAR,
    P_UPDATE_USER IN NVARCHAR2
)
AS
    V_USER_ID NUMBER;
BEGIN
    -- Get user ID for default search logic
    SELECT USER_ID INTO V_USER_ID
    FROM SYS_SAVED_SEARCH
    WHERE ROW_ID = P_ROW_ID;
    
    -- If this is set as default, unset other defaults for this user
    IF P_IS_DEFAULT = 'Y' THEN
        UPDATE SYS_SAVED_SEARCH
        SET IS_DEFAULT = 'N',
            UPDATE_USER = P_UPDATE_USER,
            UPDATE_DATE = SYSDATE
        WHERE USER_ID = V_USER_ID AND IS_DEFAULT = 'Y' AND ROW_ID != P_ROW_ID;
    END IF;
    
    -- Update saved search
    UPDATE SYS_SAVED_SEARCH
    SET 
        SEARCH_NAME = P_SEARCH_NAME,
        SEARCH_DESCRIPTION = P_SEARCH_DESCRIPTION,
        SEARCH_CRITERIA = P_SEARCH_CRITERIA,
        IS_PUBLIC = P_IS_PUBLIC,
        IS_DEFAULT = P_IS_DEFAULT,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20451, 'No saved search found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20452, 'Error updating saved search: ' || SQLERRM);
END SP_SYS_SAVED_SEARCH_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_SAVED_SEARCH_SELECT_BY_USER
-- Description: Retrieves all saved searches for a user (private + public)
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_SAVED_SEARCH_SELECT_BY_USER (
    P_USER_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        s.ROW_ID,
        s.USER_ID,
        u.ROW_DESC_E AS USER_NAME,
        s.SEARCH_NAME,
        s.SEARCH_DESCRIPTION,
        s.SEARCH_CRITERIA,
        s.IS_PUBLIC,
        s.IS_DEFAULT,
        s.USAGE_COUNT,
        s.LAST_USED_DATE,
        s.IS_ACTIVE,
        s.CREATION_USER,
        s.CREATION_DATE,
        s.UPDATE_USER,
        s.UPDATE_DATE
    FROM SYS_SAVED_SEARCH s
    LEFT JOIN SYS_USERS u ON s.USER_ID = u.ROW_ID
    WHERE s.IS_ACTIVE = 'Y'
      AND (s.USER_ID = P_USER_ID OR s.IS_PUBLIC = 'Y')
    ORDER BY s.IS_DEFAULT DESC, s.USAGE_COUNT DESC, s.SEARCH_NAME;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20453, 'Error retrieving saved searches: ' || SQLERRM);
END SP_SYS_SAVED_SEARCH_SELECT_BY_USER;
/

-- =============================================
-- Procedure: SP_SYS_SAVED_SEARCH_SELECT_BY_ID
-- Description: Retrieves a specific saved search by ID
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_SAVED_SEARCH_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        s.ROW_ID,
        s.USER_ID,
        u.ROW_DESC_E AS USER_NAME,
        s.SEARCH_NAME,
        s.SEARCH_DESCRIPTION,
        s.SEARCH_CRITERIA,
        s.IS_PUBLIC,
        s.IS_DEFAULT,
        s.USAGE_COUNT,
        s.LAST_USED_DATE,
        s.IS_ACTIVE,
        s.CREATION_USER,
        s.CREATION_DATE,
        s.UPDATE_USER,
        s.UPDATE_DATE
    FROM SYS_SAVED_SEARCH s
    LEFT JOIN SYS_USERS u ON s.USER_ID = u.ROW_ID
    WHERE s.ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20454, 'Error retrieving saved search: ' || SQLERRM);
END SP_SYS_SAVED_SEARCH_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_SAVED_SEARCH_DELETE
-- Description: Soft deletes a saved search
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_SAVED_SEARCH_DELETE (
    P_ROW_ID IN NUMBER,
    P_DELETE_USER IN NVARCHAR2
)
AS
BEGIN
    UPDATE SYS_SAVED_SEARCH
    SET 
        IS_ACTIVE = 'N',
        UPDATE_USER = P_DELETE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20455, 'No saved search found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20456, 'Error deleting saved search: ' || SQLERRM);
END SP_SYS_SAVED_SEARCH_DELETE;
/

-- =============================================
-- Procedure: SP_SYS_SAVED_SEARCH_INCREMENT_USAGE
-- Description: Increments usage count and updates last used date
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_SAVED_SEARCH_INCREMENT_USAGE (
    P_ROW_ID IN NUMBER
)
AS
BEGIN
    UPDATE SYS_SAVED_SEARCH
    SET 
        USAGE_COUNT = USAGE_COUNT + 1,
        LAST_USED_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20457, 'Error updating saved search usage: ' || SQLERRM);
END SP_SYS_SAVED_SEARCH_INCREMENT_USAGE;
/

-- =============================================
-- Verification
-- =============================================
SELECT 'Table created: SYS_SAVED_SEARCH' AS status FROM DUAL
WHERE EXISTS (SELECT 1 FROM user_tables WHERE table_name = 'SYS_SAVED_SEARCH');

SELECT 'Sequence created: SEQ_SYS_SAVED_SEARCH' AS status FROM DUAL
WHERE EXISTS (SELECT 1 FROM user_sequences WHERE sequence_name = 'SEQ_SYS_SAVED_SEARCH');

SELECT object_name, object_type, status
FROM user_objects
WHERE object_name LIKE 'SP_SYS_SAVED_SEARCH%'
ORDER BY object_name;
