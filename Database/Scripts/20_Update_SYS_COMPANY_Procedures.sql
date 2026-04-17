-- =============================================
-- ThinkOnErp API - Update SYS_COMPANY Stored Procedures
-- Description: Updates CRUD stored procedures to include new columns
-- =============================================

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_ALL (Updated)
-- Description: Retrieves all active companies with new columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        DEFAULT_LANG,
        TAX_NUMBER,
        FISCAL_YEAR_ID,
        BASE_CURRENCY_ID,
        SYSTEM_LANGUAGE,
        ROUNDING_RULES,
        COUNTRY_ID,
        CURR_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_COMPANY
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving companies: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_BY_ID (Updated)
-- Description: Retrieves a specific company by ID with new columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_BY_ID (
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
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        DEFAULT_LANG,
        TAX_NUMBER,
        FISCAL_YEAR_ID,
        BASE_CURRENCY_ID,
        SYSTEM_LANGUAGE,
        ROUNDING_RULES,
        COUNTRY_ID,
        CURR_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_COMPANY
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving company by ID: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_INSERT (Updated)
-- Description: Inserts a new company record with new columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_INSERT (
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_DEFAULT_LANG IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2,
    P_FISCAL_YEAR_ID IN NUMBER,
    P_BASE_CURRENCY_ID IN NUMBER,
    P_SYSTEM_LANGUAGE IN VARCHAR2,
    P_ROUNDING_RULES IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Generate new ID from sequence
    SELECT SEQ_SYS_COMPANY.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new company record
    INSERT INTO SYS_COMPANY (
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        DEFAULT_LANG,
        TAX_NUMBER,
        FISCAL_YEAR_ID,
        BASE_CURRENCY_ID,
        SYSTEM_LANGUAGE,
        ROUNDING_RULES,
        COUNTRY_ID,
        CURR_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_ROW_DESC,
        P_ROW_DESC_E,
        P_LEGAL_NAME,
        P_LEGAL_NAME_E,
        P_COMPANY_CODE,
        NVL(P_DEFAULT_LANG, 'ar'),
        P_TAX_NUMBER,
        P_FISCAL_YEAR_ID,
        P_BASE_CURRENCY_ID,
        NVL(P_SYSTEM_LANGUAGE, 'ar'),
        NVL(P_ROUNDING_RULES, 'HALF_UP'),
        P_COUNTRY_ID,
        P_CURR_ID,
        '1',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20203, 'Company code already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20204, 'Error inserting company: ' || SQLERRM);
END SP_SYS_COMPANY_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_UPDATE (Updated)
-- Description: Updates an existing company record with new columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_DEFAULT_LANG IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2,
    P_FISCAL_YEAR_ID IN NUMBER,
    P_BASE_CURRENCY_ID IN NUMBER,
    P_SYSTEM_LANGUAGE IN VARCHAR2,
    P_ROUNDING_RULES IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the company record
    UPDATE SYS_COMPANY
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        LEGAL_NAME = P_LEGAL_NAME,
        LEGAL_NAME_E = P_LEGAL_NAME_E,
        COMPANY_CODE = P_COMPANY_CODE,
        DEFAULT_LANG = P_DEFAULT_LANG,
        TAX_NUMBER = P_TAX_NUMBER,
        FISCAL_YEAR_ID = P_FISCAL_YEAR_ID,
        BASE_CURRENCY_ID = P_BASE_CURRENCY_ID,
        SYSTEM_LANGUAGE = P_SYSTEM_LANGUAGE,
        ROUNDING_RULES = P_ROUNDING_RULES,
        COUNTRY_ID = P_COUNTRY_ID,
        CURR_ID = P_CURR_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20205, 'No company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20206, 'Company code already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20207, 'Error updating company: ' || SQLERRM);
END SP_SYS_COMPANY_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_UPDATE_LOGO
-- Description: Updates company logo separately (BLOB handling)
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE_LOGO (
    P_ROW_ID IN NUMBER,
    P_COMPANY_LOGO IN BLOB,
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the company logo
    UPDATE SYS_COMPANY
    SET 
        COMPANY_LOGO = P_COMPANY_LOGO,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20208, 'No company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20209, 'Error updating company logo: ' || SQLERRM);
END SP_SYS_COMPANY_UPDATE_LOGO;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_GET_LOGO
-- Description: Retrieves company logo
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_GET_LOGO (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        COMPANY_LOGO
    FROM SYS_COMPANY
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20210, 'Error retrieving company logo: ' || SQLERRM);
END SP_SYS_COMPANY_GET_LOGO;
/

-- =============================================
-- Verification: Display all updated procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_SELECT_ALL',
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_COMPANY_INSERT',
    'SP_SYS_COMPANY_UPDATE',
    'SP_SYS_COMPANY_DELETE',
    'SP_SYS_COMPANY_UPDATE_LOGO',
    'SP_SYS_COMPANY_GET_LOGO'
)
ORDER BY object_name;
