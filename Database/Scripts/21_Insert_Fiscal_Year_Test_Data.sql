-- =============================================
-- ThinkOnErp API - Fiscal Year Test Data
-- Description: Inserts test data for fiscal years
-- Note: Run this after creating companies
-- =============================================

DECLARE
    v_new_id NUMBER;
BEGIN
    -- Fiscal Year for Company 1 (2024)
    SP_SYS_FISCAL_YEAR_INSERT(
        P_COMPANY_ID => 1,
        P_FISCAL_YEAR_CODE => 'FY2024',
        P_ROW_DESC => 'السنة المالية 2024',
        P_ROW_DESC_E => 'Fiscal Year 2024',
        P_START_DATE => TO_DATE('2024-01-01', 'YYYY-MM-DD'),
        P_END_DATE => TO_DATE('2024-12-31', 'YYYY-MM-DD'),
        P_IS_CLOSED => '0',
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
    DBMS_OUTPUT.PUT_LINE('Created Fiscal Year ID: ' || v_new_id);
    
    -- Fiscal Year for Company 1 (2025)
    SP_SYS_FISCAL_YEAR_INSERT(
        P_COMPANY_ID => 1,
        P_FISCAL_YEAR_CODE => 'FY2025',
        P_ROW_DESC => 'السنة المالية 2025',
        P_ROW_DESC_E => 'Fiscal Year 2025',
        P_START_DATE => TO_DATE('2025-01-01', 'YYYY-MM-DD'),
        P_END_DATE => TO_DATE('2025-12-31', 'YYYY-MM-DD'),
        P_IS_CLOSED => '0',
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
    DBMS_OUTPUT.PUT_LINE('Created Fiscal Year ID: ' || v_new_id);
    
    -- Fiscal Year for Company 1 (2026)
    SP_SYS_FISCAL_YEAR_INSERT(
        P_COMPANY_ID => 1,
        P_FISCAL_YEAR_CODE => 'FY2026',
        P_ROW_DESC => 'السنة المالية 2026',
        P_ROW_DESC_E => 'Fiscal Year 2026',
        P_START_DATE => TO_DATE('2026-01-01', 'YYYY-MM-DD'),
        P_END_DATE => TO_DATE('2026-12-31', 'YYYY-MM-DD'),
        P_IS_CLOSED => '0',
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
    DBMS_OUTPUT.PUT_LINE('Created Fiscal Year ID: ' || v_new_id);
    
    -- Fiscal Year for Company 2 (2024)
    SP_SYS_FISCAL_YEAR_INSERT(
        P_COMPANY_ID => 2,
        P_FISCAL_YEAR_CODE => 'FY2024',
        P_ROW_DESC => 'السنة المالية 2024',
        P_ROW_DESC_E => 'Fiscal Year 2024',
        P_START_DATE => TO_DATE('2024-01-01', 'YYYY-MM-DD'),
        P_END_DATE => TO_DATE('2024-12-31', 'YYYY-MM-DD'),
        P_IS_CLOSED => '1',
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
    DBMS_OUTPUT.PUT_LINE('Created Fiscal Year ID: ' || v_new_id);
    
    -- Fiscal Year for Company 2 (2025)
    SP_SYS_FISCAL_YEAR_INSERT(
        P_COMPANY_ID => 2,
        P_FISCAL_YEAR_CODE => 'FY2025',
        P_ROW_DESC => 'السنة المالية 2025',
        P_ROW_DESC_E => 'Fiscal Year 2025',
        P_START_DATE => TO_DATE('2025-01-01', 'YYYY-MM-DD'),
        P_END_DATE => TO_DATE('2025-12-31', 'YYYY-MM-DD'),
        P_IS_CLOSED => '0',
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
    DBMS_OUTPUT.PUT_LINE('Created Fiscal Year ID: ' || v_new_id);
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Test fiscal year data inserted successfully!');
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('Error: ' || SQLERRM);
        RAISE;
END;
/

-- Verification: Display all fiscal years
SELECT 
    fy.ROW_ID,
    fy.COMPANY_ID,
    c.ROW_DESC_E AS COMPANY_NAME,
    fy.FISCAL_YEAR_CODE,
    fy.ROW_DESC_E,
    fy.START_DATE,
    fy.END_DATE,
    fy.IS_CLOSED,
    fy.IS_ACTIVE
FROM SYS_FISCAL_YEAR fy
JOIN SYS_COMPANY c ON fy.COMPANY_ID = c.ROW_ID
ORDER BY fy.COMPANY_ID, fy.START_DATE;
