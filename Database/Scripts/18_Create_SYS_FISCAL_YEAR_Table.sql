-- =============================================
-- ThinkOnErp API - SYS_FISCAL_YEAR Table and Procedures
-- Description: Creates fiscal year table, sequence, and CRUD stored procedures
-- =============================================

-- =============================================
-- Create Sequence for SYS_FISCAL_YEAR
-- =============================================
CREATE SEQUENCE SEQ_SYS_FISCAL_YEAR
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- =============================================
-- Create SYS_FISCAL_YEAR Table
-- Description: Stores fiscal year information for companies
-- =============================================
CREATE TABLE SYS_FISCAL_YEAR (
    ROW_ID NUMBER PRIMARY KEY,
    COMPANY_ID NUMBER NOT NULL,
    FISCAL_YEAR_CODE VARCHAR2(20) NOT NULL,
    ROW_DESC VARCHAR2(200),
    ROW_DESC_E VARCHAR2(200),
    START_DATE DATE NOT NULL,
    END_DATE DATE NOT NULL,
    IS_CLOSED CHAR(1) DEFAULT '0' CHECK (IS_CLOSED IN ('0', '1')),
    IS_ACTIVE CHAR(1) DEFAULT '1' CHECK (IS_ACTIVE IN ('0', '1')),
    CREATION_USER VARCHAR2(100),
    CREATION_DATE DATE DEFAULT SYSDATE,
    UPDATE_USER VARCHAR2(100),
    UPDATE_DATE DATE,
    CONSTRAINT FK_FISCAL_YEAR_COMPANY FOREIGN KEY (COMPANY_ID) REFERENCES SYS_COMPANY(ROW_ID),
    CONSTRAINT UK_FISCAL_YEAR_CODE UNIQUE (COMPANY_ID, FISCAL_YEAR_CODE)
);

-- Create index for better query performance
CREATE INDEX IDX_FISCAL_YEAR_COMPANY ON SYS_FISCAL_YEAR(COMPANY_ID);
CREATE INDEX IDX_FISCAL_YEAR_DATES ON SYS_FISCAL_YEAR(START_DATE, END_DATE);

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_SELECT_ALL
-- Description: Retrieves all active fiscal years
-- Returns: SYS_REFCURSOR with all fiscal years where IS_ACTIVE = '1'
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        COMPANY_ID,
        FISCAL_YEAR_CODE,
        ROW_DESC,
        ROW_DESC_E,
        START_DATE,
        END_DATE,
        IS_CLOSED,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_FISCAL_YEAR
    WHERE IS_ACTIVE = '1'
    ORDER BY COMPANY_ID, START_DATE DESC;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20301, 'Error retrieving fiscal years: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_SELECT_BY_ID
-- Description: Retrieves a specific fiscal year by ID
-- Parameters:
--   P_ROW_ID: The fiscal year ID to retrieve
-- Returns: SYS_REFCURSOR with the matching fiscal year
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        COMPANY_ID,
        FISCAL_YEAR_CODE,
        ROW_DESC,
        ROW_DESC_E,
        START_DATE,
        END_DATE,
        IS_CLOSED,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_FISCAL_YEAR
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20302, 'Error retrieving fiscal year by ID: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY
-- Description: Retrieves all fiscal years for a specific company
-- Parameters:
--   P_COMPANY_ID: The company ID to retrieve fiscal years for
-- Returns: SYS_REFCURSOR with matching fiscal years
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY (
    P_COMPANY_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        COMPANY_ID,
        FISCAL_YEAR_CODE,
        ROW_DESC,
        ROW_DESC_E,
        START_DATE,
        END_DATE,
        IS_CLOSED,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_FISCAL_YEAR
    WHERE COMPANY_ID = P_COMPANY_ID
    AND IS_ACTIVE = '1'
    ORDER BY START_DATE DESC;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20303, 'Error retrieving fiscal years by company: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_INSERT
-- Description: Inserts a new fiscal year record
-- Parameters:
--   P_COMPANY_ID: Company ID (foreign key to SYS_COMPANY)
--   P_FISCAL_YEAR_CODE: Fiscal year code (e.g., 'FY2024')
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_START_DATE: Fiscal year start date
--   P_END_DATE: Fiscal year end date
--   P_IS_CLOSED: Closed flag ('1' or '0')
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new fiscal year ID
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_INSERT (
    P_COMPANY_ID IN NUMBER,
    P_FISCAL_YEAR_CODE IN VARCHAR2,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_IS_CLOSED IN CHAR,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Validate date range
    IF P_END_DATE <= P_START_DATE THEN
        RAISE_APPLICATION_ERROR(-20304, 'End date must be after start date');
    END IF;
    
    -- Generate new ID from sequence
    SELECT SEQ_SYS_FISCAL_YEAR.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new fiscal year record
    INSERT INTO SYS_FISCAL_YEAR (
        ROW_ID,
        COMPANY_ID,
        FISCAL_YEAR_CODE,
        ROW_DESC,
        ROW_DESC_E,
        START_DATE,
        END_DATE,
        IS_CLOSED,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_COMPANY_ID,
        P_FISCAL_YEAR_CODE,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_START_DATE,
        P_END_DATE,
        NVL(P_IS_CLOSED, '0'),
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20305, 'Fiscal year code already exists for this company');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20306, 'Error inserting fiscal year: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_UPDATE
-- Description: Updates an existing fiscal year record
-- Parameters:
--   P_ROW_ID: The fiscal year ID to update
--   P_COMPANY_ID: Company ID (foreign key to SYS_COMPANY)
--   P_FISCAL_YEAR_CODE: Fiscal year code
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_START_DATE: Fiscal year start date
--   P_END_DATE: Fiscal year end date
--   P_IS_CLOSED: Closed flag ('1' or '0')
--   P_UPDATE_USER: User updating the record
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_UPDATE (
    P_ROW_ID IN NUMBER,
    P_COMPANY_ID IN NUMBER,
    P_FISCAL_YEAR_CODE IN VARCHAR2,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_IS_CLOSED IN CHAR,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Validate date range
    IF P_END_DATE <= P_START_DATE THEN
        RAISE_APPLICATION_ERROR(-20307, 'End date must be after start date');
    END IF;
    
    -- Update the fiscal year record
    UPDATE SYS_FISCAL_YEAR
    SET 
        COMPANY_ID = P_COMPANY_ID,
        FISCAL_YEAR_CODE = P_FISCAL_YEAR_CODE,
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        START_DATE = P_START_DATE,
        END_DATE = P_END_DATE,
        IS_CLOSED = P_IS_CLOSED,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20308, 'No fiscal year found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20309, 'Fiscal year code already exists for this company');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20310, 'Error updating fiscal year: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_DELETE
-- Description: Soft deletes a fiscal year by setting IS_ACTIVE to '0'
-- Parameters:
--   P_ROW_ID: The fiscal year ID to delete
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_DELETE (
    P_ROW_ID IN NUMBER
)
AS
BEGIN
    -- Soft delete by setting IS_ACTIVE to '0'
    UPDATE SYS_FISCAL_YEAR
    SET IS_ACTIVE = '0'
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20311, 'No fiscal year found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20312, 'Error deleting fiscal year: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_DELETE;
/

-- =============================================
-- Procedure: SP_SYS_FISCAL_YEAR_CLOSE
-- Description: Closes a fiscal year by setting IS_CLOSED to '1'
-- Parameters:
--   P_ROW_ID: The fiscal year ID to close
--   P_UPDATE_USER: User closing the fiscal year
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_FISCAL_YEAR_CLOSE (
    P_ROW_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Close the fiscal year
    UPDATE SYS_FISCAL_YEAR
    SET 
        IS_CLOSED = '1',
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20313, 'No fiscal year found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20314, 'Error closing fiscal year: ' || SQLERRM);
END SP_SYS_FISCAL_YEAR_CLOSE;
/

-- =============================================
-- Verification: Display created objects
-- =============================================
SELECT 'Sequence' AS object_type, sequence_name AS object_name
FROM user_sequences
WHERE sequence_name = 'SEQ_SYS_FISCAL_YEAR'
UNION ALL
SELECT 'Table' AS object_type, table_name AS object_name
FROM user_tables
WHERE table_name = 'SYS_FISCAL_YEAR'
UNION ALL
SELECT object_type, object_name
FROM user_objects
WHERE object_name IN (
    'SP_SYS_FISCAL_YEAR_SELECT_ALL',
    'SP_SYS_FISCAL_YEAR_SELECT_BY_ID',
    'SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY',
    'SP_SYS_FISCAL_YEAR_INSERT',
    'SP_SYS_FISCAL_YEAR_UPDATE',
    'SP_SYS_FISCAL_YEAR_DELETE',
    'SP_SYS_FISCAL_YEAR_CLOSE'
)
ORDER BY object_type, object_name;
