-- =============================================
-- ThinkOnErp API - Update Company Test Data
-- Description: Updates existing company records with new column values
-- Note: Run this after extending the SYS_COMPANY table and creating fiscal years
-- =============================================

BEGIN
    -- Update Company 1 with new fields
    UPDATE SYS_COMPANY
    SET 
        LEGAL_NAME = 'شركة ثينك أون للبرمجيات المحدودة',
        LEGAL_NAME_E = 'ThinkOn Software Solutions Ltd.',
        COMPANY_CODE = 'COMP001',
        DEFAULT_LANG = 'ar',
        TAX_NUMBER = '300123456789003',
        FISCAL_YEAR_ID = 3, -- FY2026 for Company 1
        BASE_CURRENCY_ID = 1, -- Assuming SAR is ID 1
        SYSTEM_LANGUAGE = 'ar',
        ROUNDING_RULES = 'HALF_UP',
        UPDATE_USER = 'admin',
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = 1;
    
    DBMS_OUTPUT.PUT_LINE('Updated Company ID: 1');
    
    -- Update Company 2 with new fields
    UPDATE SYS_COMPANY
    SET 
        LEGAL_NAME = 'شركة التجارة العالمية المحدودة',
        LEGAL_NAME_E = 'Global Trading Company Ltd.',
        COMPANY_CODE = 'COMP002',
        DEFAULT_LANG = 'en',
        TAX_NUMBER = '300987654321003',
        FISCAL_YEAR_ID = 5, -- FY2025 for Company 2
        BASE_CURRENCY_ID = 2, -- Assuming USD is ID 2
        SYSTEM_LANGUAGE = 'en',
        ROUNDING_RULES = 'HALF_UP',
        UPDATE_USER = 'admin',
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = 2;
    
    DBMS_OUTPUT.PUT_LINE('Updated Company ID: 2');
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Company test data updated successfully!');
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('Error: ' || SQLERRM);
        RAISE;
END;
/

-- Verification: Display updated companies with new fields
SELECT 
    c.ROW_ID,
    c.ROW_DESC_E AS COMPANY_NAME,
    c.LEGAL_NAME_E,
    c.COMPANY_CODE,
    c.TAX_NUMBER,
    c.DEFAULT_LANG,
    c.SYSTEM_LANGUAGE,
    c.ROUNDING_RULES,
    fy.FISCAL_YEAR_CODE,
    curr.ROW_DESC_E AS BASE_CURRENCY,
    c.IS_ACTIVE
FROM SYS_COMPANY c
LEFT JOIN SYS_FISCAL_YEAR fy ON c.FISCAL_YEAR_ID = fy.ROW_ID
LEFT JOIN SYS_CURRENCY curr ON c.BASE_CURRENCY_ID = curr.ROW_ID
WHERE c.IS_ACTIVE = '1'
ORDER BY c.ROW_ID;
