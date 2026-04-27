-- =============================================
-- Script: 46_Fix_Company_Select_Procedures.sql
-- Description: Fix SP_SYS_COMPANY_SELECT_ALL and SP_SYS_COMPANY_SELECT_BY_ID to match current table structure
-- Removes references to columns that were moved to branches or removed
-- Author: System
-- Date: 2026-04-26
-- =============================================

PROMPT ========================================
PROMPT Fixing Company SELECT Procedures
PROMPT ========================================

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_ALL
-- Description: Retrieves all active companies (updated to match current table structure)
-- Removed columns: DEFAULT_LANG, FISCAL_YEAR_ID, BASE_CURRENCY_ID, SYSTEM_LANGUAGE, ROUNDING_RULES
-- Added column: DEFAULT_BRANCH_ID
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
        TAX_NUMBER,
        COUNTRY_ID,
        CURR_ID,
        DEFAULT_BRANCH_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving companies: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_COMPANY_SELECT_BY_ID
-- Description: Retrieves a specific company by ID (updated to match current table structure)
-- Removed columns: DEFAULT_LANG, FISCAL_YEAR_ID, BASE_CURRENCY_ID, SYSTEM_LANGUAGE, ROUNDING_RULES
-- Added column: DEFAULT_BRANCH_ID
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
        TAX_NUMBER,
        COUNTRY_ID,
        CURR_ID,
        DEFAULT_BRANCH_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving company by ID: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_BY_ID;
/

PROMPT ========================================
PROMPT Procedures Updated Successfully
PROMPT ========================================

-- Verification: Display procedure status
SELECT object_name, object_type, status, last_ddl_time
FROM user_objects
WHERE object_name IN ('SP_SYS_COMPANY_SELECT_ALL', 'SP_SYS_COMPANY_SELECT_BY_ID')
ORDER BY object_name;

PROMPT '';
PROMPT 'Columns now returned by SELECT procedures:';
PROMPT '- ROW_ID';
PROMPT '- ROW_DESC';
PROMPT '- ROW_DESC_E';
PROMPT '- LEGAL_NAME';
PROMPT '- LEGAL_NAME_E';
PROMPT '- COMPANY_CODE';
PROMPT '- TAX_NUMBER';
PROMPT '- COUNTRY_ID';
PROMPT '- CURR_ID';
PROMPT '- DEFAULT_BRANCH_ID (NEW)';
PROMPT '- IS_ACTIVE';
PROMPT '- CREATION_USER';
PROMPT '- CREATION_DATE';
PROMPT '- UPDATE_USER';
PROMPT '- UPDATE_DATE';
PROMPT '- HAS_LOGO';
PROMPT '';
PROMPT 'Removed columns (moved to branches or deleted):';
PROMPT '- DEFAULT_LANG (moved to SYS_BRANCH)';
PROMPT '- FISCAL_YEAR_ID (removed - fiscal years now have COMPANY_ID and BRANCH_ID)';
PROMPT '- BASE_CURRENCY_ID (moved to SYS_BRANCH)';
PROMPT '- SYSTEM_LANGUAGE (removed)';
PROMPT '- ROUNDING_RULES (moved to SYS_BRANCH)';
PROMPT '';

PROMPT ========================================
PROMPT Fix Complete
PROMPT ========================================
