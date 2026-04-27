-- =====================================================
-- FIX: Branch Procedures Column Mismatch
-- =====================================================
-- This script updates SP_SYS_BRANCH_SELECT_ALL and SP_SYS_BRANCH_SELECT_BY_ID
-- to include the missing columns that were added to SYS_BRANCH table:
-- - DEFAULT_LANG
-- - BASE_CURRENCY_ID
-- - ROUNDING_RULES
-- - HAS_LOGO (computed column)
-- =====================================================

PROMPT =====================================================
PROMPT Fixing Branch Procedures Column Mismatch
PROMPT =====================================================

-- =============================================
-- Procedure: SP_SYS_BRANCH_SELECT_ALL
-- Description: Retrieves all active branches with all columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        PAR_ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        PHONE,
        MOBILE,
        FAX,
        EMAIL,
        IS_HEAD_BRANCH,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        DEFAULT_LANG,
        BASE_CURRENCY_ID,
        ROUNDING_RULES,
        CASE 
            WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving branches: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_ALL;
/

PROMPT ✓ Updated SP_SYS_BRANCH_SELECT_ALL procedure
PROMPT

-- =============================================
-- Procedure: SP_SYS_BRANCH_SELECT_BY_ID
-- Description: Retrieves a specific branch by ID with all columns
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        PAR_ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        PHONE,
        MOBILE,
        FAX,
        EMAIL,
        IS_HEAD_BRANCH,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        DEFAULT_LANG,
        BASE_CURRENCY_ID,
        ROUNDING_RULES,
        CASE 
            WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving branch by ID: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_BY_ID;
/

PROMPT ✓ Updated SP_SYS_BRANCH_SELECT_BY_ID procedure
PROMPT

-- Verify the procedures were created successfully
PROMPT Verifying procedures:
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN ('SP_SYS_BRANCH_SELECT_ALL', 'SP_SYS_BRANCH_SELECT_BY_ID')
ORDER BY object_name;

PROMPT
PROMPT =====================================================
PROMPT Fix completed successfully!
PROMPT =====================================================
PROMPT
PROMPT The procedures now return:
PROMPT - DEFAULT_LANG
PROMPT - BASE_CURRENCY_ID
PROMPT - ROUNDING_RULES
PROMPT - HAS_LOGO (computed column)
PROMPT
PROMPT =====================================================
