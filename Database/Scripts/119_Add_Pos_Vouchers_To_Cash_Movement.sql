-- ==============================================================================
-- 119_Add_Pos_Vouchers_To_Cash_Movement.sql
-- Adds Payment Method, Voucher Type, GL Account, Beneficiary, Tax Invoice,
-- and GL Voucher linking to POS_SHIFT_CASH_MOVEMENT across all schemas
-- ==============================================================================

DECLARE
    v_col_count NUMBER;
BEGIN
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'POS_SHIFT_CASH_MOVEMENT' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%')
        ORDER BY owner
    ) LOOP
        -- PAYMENT_METHOD
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT_CASH_MOVEMENT' AND column_name = 'PAYMENT_METHOD';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT_CASH_MOVEMENT" ADD ("PAYMENT_METHOD" NUMBER(10) DEFAULT 1 NOT NULL)';
        END IF;

        -- VOUCHER_TYPE
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT_CASH_MOVEMENT' AND column_name = 'VOUCHER_TYPE';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT_CASH_MOVEMENT" ADD ("VOUCHER_TYPE" NUMBER(10) DEFAULT 2 NOT NULL)';
        END IF;

        -- VOUCHER_NUMBER
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT_CASH_MOVEMENT' AND column_name = 'VOUCHER_NUMBER';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT_CASH_MOVEMENT" ADD ("VOUCHER_NUMBER" NVARCHAR2(50))';
        END IF;

        -- GL_ACCOUNT_CODE
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT_CASH_MOVEMENT' AND column_name = 'GL_ACCOUNT_CODE';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT_CASH_MOVEMENT" ADD ("GL_ACCOUNT_CODE" NVARCHAR2(50))';
        END IF;

        -- BENEFICIARY_NAME
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT_CASH_MOVEMENT' AND column_name = 'BENEFICIARY_NAME';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT_CASH_MOVEMENT" ADD ("BENEFICIARY_NAME" NVARCHAR2(200))';
        END IF;

        -- TAX_INVOICE_NUMBER
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT_CASH_MOVEMENT' AND column_name = 'TAX_INVOICE_NUMBER';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT_CASH_MOVEMENT" ADD ("TAX_INVOICE_NUMBER" NVARCHAR2(100))';
        END IF;

        -- GL_VOUCHER_ID
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT_CASH_MOVEMENT' AND column_name = 'GL_VOUCHER_ID';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT_CASH_MOVEMENT" ADD ("GL_VOUCHER_ID" NUMBER(19))';
        END IF;
    END LOOP;

    COMMIT;
END;
/
EXIT;
