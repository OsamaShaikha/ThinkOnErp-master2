-- =============================================
-- ThinkOnErp API - SYS_CURRENCY Stored Procedures
-- Description: CRUD stored procedures for SYS_CURRENCY table
-- Requirements: 18.1, 18.2, 18.3, 18.4, 18.5, 18.6, 18.7, 18.8
-- =============================================

-- =============================================
-- Procedure: SP_SYS_CURRENCY_SELECT_ALL
-- Description: Retrieves all active currencies
-- Returns: SYS_REFCURSOR with all currencies where IS_ACTIVE = '1'
-- Requirements: 18.1
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_CURRENCY_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        SHORT_DESC,
        SHORT_DESC_E,
        SINGULER_DESC,
        SINGULER_DESC_E,
        DUAL_DESC,
        DUAL_DESC_E,
        SUM_DESC,
        SUM_DESC_E,
        FRAC_DESC,
        FRAC_DESC_E,
        CURR_RATE,
        CURR_RATE_DATE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_CURRENCY
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20101, 'Error retrieving currencies: ' || SQLERRM);
END SP_SYS_CURRENCY_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_CURRENCY_SELECT_BY_ID
-- Description: Retrieves a specific currency by ID
-- Parameters:
--   P_ROW_ID: The currency ID to retrieve
-- Returns: SYS_REFCURSOR with the matching currency
-- Requirements: 18.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_CURRENCY_SELECT_BY_ID (
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
        SHORT_DESC,
        SHORT_DESC_E,
        SINGULER_DESC,
        SINGULER_DESC_E,
        DUAL_DESC,
        DUAL_DESC_E,
        SUM_DESC,
        SUM_DESC_E,
        FRAC_DESC,
        FRAC_DESC_E,
        CURR_RATE,
        CURR_RATE_DATE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_CURRENCY
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20102, 'Error retrieving currency by ID: ' || SQLERRM);
END SP_SYS_CURRENCY_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_CURRENCY_INSERT
-- Description: Inserts a new currency record
-- Parameters:
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_SHORT_DESC: Arabic short description
--   P_SHORT_DESC_E: English short description
--   P_SINGULER_DESC: Arabic singular description
--   P_SINGULER_DESC_E: English singular description
--   P_DUAL_DESC: Arabic dual description
--   P_DUAL_DESC_E: English dual description
--   P_SUM_DESC: Arabic sum description
--   P_SUM_DESC_E: English sum description
--   P_FRAC_DESC: Arabic fraction description
--   P_FRAC_DESC_E: English fraction description
--   P_CURR_RATE: Exchange rate (optional)
--   P_CURR_RATE_DATE: Exchange rate date (optional)
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new currency ID
-- Requirements: 18.3, 18.4, 28.1, 28.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_CURRENCY_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_SHORT_DESC IN VARCHAR2,
    P_SHORT_DESC_E IN VARCHAR2,
    P_SINGULER_DESC IN VARCHAR2,
    P_SINGULER_DESC_E IN VARCHAR2,
    P_DUAL_DESC IN VARCHAR2,
    P_DUAL_DESC_E IN VARCHAR2,
    P_SUM_DESC IN VARCHAR2,
    P_SUM_DESC_E IN VARCHAR2,
    P_FRAC_DESC IN VARCHAR2,
    P_FRAC_DESC_E IN VARCHAR2,
    P_CURR_RATE IN NUMBER,
    P_CURR_RATE_DATE IN DATE,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Generate new ID from sequence
    SELECT SEQ_SYS_CURRENCY.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new currency record
    INSERT INTO SYS_CURRENCY (
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        SHORT_DESC,
        SHORT_DESC_E,
        SINGULER_DESC,
        SINGULER_DESC_E,
        DUAL_DESC,
        DUAL_DESC_E,
        SUM_DESC,
        SUM_DESC_E,
        FRAC_DESC,
        FRAC_DESC_E,
        CURR_RATE,
        CURR_RATE_DATE,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_SHORT_DESC,
        P_SHORT_DESC_E,
        P_SINGULER_DESC,
        P_SINGULER_DESC_E,
        P_DUAL_DESC,
        P_DUAL_DESC_E,
        P_SUM_DESC,
        P_SUM_DESC_E,
        P_FRAC_DESC,
        P_FRAC_DESC_E,
        P_CURR_RATE,
        P_CURR_RATE_DATE,
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20103, 'Error inserting currency: ' || SQLERRM);
END SP_SYS_CURRENCY_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_CURRENCY_UPDATE
-- Description: Updates an existing currency record
-- Parameters:
--   P_ROW_ID: The currency ID to update
--   P_ROW_DESC: Arabic description
--   P_ROW_DESC_E: English description
--   P_SHORT_DESC: Arabic short description
--   P_SHORT_DESC_E: English short description
--   P_SINGULER_DESC: Arabic singular description
--   P_SINGULER_DESC_E: English singular description
--   P_DUAL_DESC: Arabic dual description
--   P_DUAL_DESC_E: English dual description
--   P_SUM_DESC: Arabic sum description
--   P_SUM_DESC_E: English sum description
--   P_FRAC_DESC: Arabic fraction description
--   P_FRAC_DESC_E: English fraction description
--   P_CURR_RATE: Exchange rate (optional)
--   P_CURR_RATE_DATE: Exchange rate date (optional)
--   P_UPDATE_USER: User updating the record
-- Requirements: 18.5, 18.6, 28.3, 28.4, 28.5
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_CURRENCY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_SHORT_DESC IN VARCHAR2,
    P_SHORT_DESC_E IN VARCHAR2,
    P_SINGULER_DESC IN VARCHAR2,
    P_SINGULER_DESC_E IN VARCHAR2,
    P_DUAL_DESC IN VARCHAR2,
    P_DUAL_DESC_E IN VARCHAR2,
    P_SUM_DESC IN VARCHAR2,
    P_SUM_DESC_E IN VARCHAR2,
    P_FRAC_DESC IN VARCHAR2,
    P_FRAC_DESC_E IN VARCHAR2,
    P_CURR_RATE IN NUMBER,
    P_CURR_RATE_DATE IN DATE,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the currency record
    UPDATE SYS_CURRENCY
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        SHORT_DESC = P_SHORT_DESC,
        SHORT_DESC_E = P_SHORT_DESC_E,
        SINGULER_DESC = P_SINGULER_DESC,
        SINGULER_DESC_E = P_SINGULER_DESC_E,
        DUAL_DESC = P_DUAL_DESC,
        DUAL_DESC_E = P_DUAL_DESC_E,
        SUM_DESC = P_SUM_DESC,
        SUM_DESC_E = P_SUM_DESC_E,
        FRAC_DESC = P_FRAC_DESC,
        FRAC_DESC_E = P_FRAC_DESC_E,
        CURR_RATE = P_CURR_RATE,
        CURR_RATE_DATE = P_CURR_RATE_DATE,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20104, 'No currency found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20105, 'Error updating currency: ' || SQLERRM);
END SP_SYS_CURRENCY_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_CURRENCY_DELETE
-- Description: Soft deletes a currency by setting IS_ACTIVE to '0'
-- Parameters:
--   P_ROW_ID: The currency ID to delete
-- Requirements: 18.7, 18.8
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_CURRENCY_DELETE (
    P_ROW_ID IN NUMBER
)
AS
BEGIN
    -- Soft delete by setting IS_ACTIVE to '0'
    UPDATE SYS_CURRENCY
    SET IS_ACTIVE = '0'
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20106, 'No currency found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20107, 'Error deleting currency: ' || SQLERRM);
END SP_SYS_CURRENCY_DELETE;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_CURRENCY_SELECT_ALL',
    'SP_SYS_CURRENCY_SELECT_BY_ID',
    'SP_SYS_CURRENCY_INSERT',
    'SP_SYS_CURRENCY_UPDATE',
    'SP_SYS_CURRENCY_DELETE'
)
ORDER BY object_name;
