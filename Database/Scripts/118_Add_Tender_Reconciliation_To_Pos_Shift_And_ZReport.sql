-- ==============================================================================
-- 118_Add_Tender_Reconciliation_To_Pos_Shift_And_ZReport.sql
-- Adds Card & Tender Reconciliation columns to POS_SHIFT and POS_Z_REPORT
-- across DEV_TEMPLATE and all tenant schemas (THINKONERP_%)
-- ==============================================================================

DECLARE
    v_col_count NUMBER;
BEGIN
    -- 1. POS_SHIFT columns
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'POS_SHIFT' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%')
        ORDER BY owner
    ) LOOP
        -- COUNTED_CARD_AMOUNT
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT' AND column_name = 'COUNTED_CARD_AMOUNT';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT" ADD ("COUNTED_CARD_AMOUNT" NUMBER(18,4))';
        END IF;

        -- CARD_VARIANCE_AMOUNT
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT' AND column_name = 'CARD_VARIANCE_AMOUNT';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT" ADD ("CARD_VARIANCE_AMOUNT" NUMBER(18,4))';
        END IF;

        -- EXPECTED_CARD_AMOUNT
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT' AND column_name = 'EXPECTED_CARD_AMOUNT';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT" ADD ("EXPECTED_CARD_AMOUNT" NUMBER(18,4) DEFAULT 0.0 NOT NULL)';
        END IF;

        -- TOTAL_CUSTOMER_ACCOUNT_SALES
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT' AND column_name = 'TOTAL_CUSTOMER_ACCOUNT_SALES';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT" ADD ("TOTAL_CUSTOMER_ACCOUNT_SALES" NUMBER(18,4) DEFAULT 0.0 NOT NULL)';
        END IF;

        -- TOTAL_HOSPITALITY_SALES
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = t.owner AND table_name = 'POS_SHIFT' AND column_name = 'TOTAL_HOSPITALITY_SALES';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_SHIFT" ADD ("TOTAL_HOSPITALITY_SALES" NUMBER(18,4) DEFAULT 0.0 NOT NULL)';
        END IF;
    END LOOP;

    -- 2. POS_Z_REPORT columns
    FOR z IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'POS_Z_REPORT' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%')
        ORDER BY owner
    ) LOOP
        -- HOSPITALITY_TOTAL
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = z.owner AND table_name = 'POS_Z_REPORT' AND column_name = 'HOSPITALITY_TOTAL';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || z.owner || '"."POS_Z_REPORT" ADD ("HOSPITALITY_TOTAL" NUMBER(18,4) DEFAULT 0.0 NOT NULL)';
        END IF;

        -- EXPECTED_CARD_AMOUNT
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = z.owner AND table_name = 'POS_Z_REPORT' AND column_name = 'EXPECTED_CARD_AMOUNT';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || z.owner || '"."POS_Z_REPORT" ADD ("EXPECTED_CARD_AMOUNT" NUMBER(18,4) DEFAULT 0.0 NOT NULL)';
        END IF;

        -- ACTUAL_COUNTED_CARD
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = z.owner AND table_name = 'POS_Z_REPORT' AND column_name = 'ACTUAL_COUNTED_CARD';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || z.owner || '"."POS_Z_REPORT" ADD ("ACTUAL_COUNTED_CARD" NUMBER(18,4) DEFAULT 0.0 NOT NULL)';
        END IF;

        -- CARD_VARIANCE
        SELECT COUNT(*) INTO v_col_count 
        FROM all_tab_columns 
        WHERE owner = z.owner AND table_name = 'POS_Z_REPORT' AND column_name = 'CARD_VARIANCE';
        IF v_col_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || z.owner || '"."POS_Z_REPORT" ADD ("CARD_VARIANCE" NUMBER(18,4) DEFAULT 0.0 NOT NULL)';
        END IF;
    END LOOP;

    COMMIT;
END;
/
EXIT;
