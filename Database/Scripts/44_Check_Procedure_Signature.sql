-- =============================================
-- Script: 44_Check_Procedure_Signature.sql
-- Description: Check the current signature of SP_SYS_COMPANY_INSERT_WITH_BRANCH
-- =============================================

PROMPT ========================================
PROMPT Checking SP_SYS_COMPANY_INSERT_WITH_BRANCH Procedure Signature
PROMPT ========================================

-- Check if procedure exists
SELECT 
    object_name,
    object_type,
    status,
    created,
    last_ddl_time
FROM user_objects
WHERE object_name = 'SP_SYS_COMPANY_INSERT_WITH_BRANCH';

PROMPT '';
PROMPT 'Procedure Parameters:';
PROMPT '';

-- Display all parameters for the procedure
SELECT 
    argument_name,
    position,
    data_type,
    in_out,
    data_length,
    data_precision,
    data_scale,
    default_value
FROM user_arguments
WHERE object_name = 'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
ORDER BY position;

PROMPT '';
PROMPT 'Expected Parameters (from C# code):';
PROMPT '1. P_ROW_DESC (IN VARCHAR2)';
PROMPT '2. P_ROW_DESC_E (IN VARCHAR2)';
PROMPT '3. P_LEGAL_NAME (IN VARCHAR2)';
PROMPT '4. P_LEGAL_NAME_E (IN VARCHAR2)';
PROMPT '5. P_COMPANY_CODE (IN VARCHAR2)';
PROMPT '6. P_TAX_NUMBER (IN VARCHAR2)';
PROMPT '7. P_COUNTRY_ID (IN NUMBER)';
PROMPT '8. P_CURR_ID (IN NUMBER)';
PROMPT '9. P_COMPANY_LOGO (IN BLOB)';
PROMPT '10. P_BRANCH_DESC (IN VARCHAR2)';
PROMPT '11. P_BRANCH_DESC_E (IN VARCHAR2)';
PROMPT '12. P_BRANCH_PHONE (IN VARCHAR2)';
PROMPT '13. P_BRANCH_MOBILE (IN VARCHAR2)';
PROMPT '14. P_BRANCH_FAX (IN VARCHAR2)';
PROMPT '15. P_BRANCH_EMAIL (IN VARCHAR2)';
PROMPT '16. P_BRANCH_LOGO (IN BLOB)';
PROMPT '17. P_DEFAULT_LANG (IN VARCHAR2)';
PROMPT '18. P_BASE_CURRENCY_ID (IN NUMBER)';
PROMPT '19. P_ROUNDING_RULES (IN NUMBER)';
PROMPT '20. P_CREATION_USER (IN VARCHAR2)';
PROMPT '21. P_NEW_COMPANY_ID (OUT NUMBER)';
PROMPT '22. P_NEW_BRANCH_ID (OUT NUMBER)';
PROMPT '23. P_NEW_FISCAL_YEAR_ID (OUT NUMBER)';
PROMPT '';

-- Show procedure source
PROMPT 'Procedure Source (first 50 lines):';
SELECT text
FROM user_source
WHERE name = 'SP_SYS_COMPANY_INSERT_WITH_BRANCH'
  AND type = 'PROCEDURE'
  AND line <= 50
ORDER BY line;

PROMPT ========================================
PROMPT Check Complete
PROMPT ========================================
