-- ==============================================================================
-- 111_Add_DefaultSellingPrice_And_ShowInPos_To_INV_ITEM.sql
-- Adds DEFAULT_SELLING_PRICE and SHOW_IN_POS to INV_ITEM in DEV_TEMPLATE and all tenant schemas
-- ==============================================================================

DECLARE
    v_sql VARCHAR2(1000);
    v_col_count NUMBER;
BEGIN
    FOR t IN (
        SELECT owner, table_name 
        FROM all_tables 
        WHERE table_name = 'INV_ITEM' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        -- 1. Check and add DEFAULT_SELLING_PRICE
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner AND table_name = t.table_name AND column_name = 'DEFAULT_SELLING_PRICE';

        IF v_col_count = 0 THEN
            v_sql := 'ALTER TABLE "' || t.owner || '"."' || t.table_name || '" ADD ("DEFAULT_SELLING_PRICE" NUMBER(18,4) DEFAULT 0 NOT NULL)';
            BEGIN
                EXECUTE IMMEDIATE v_sql;
                DBMS_OUTPUT.PUT_LINE('Added DEFAULT_SELLING_PRICE to ' || t.owner || '.' || t.table_name);
            EXCEPTION
                WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('Error adding DEFAULT_SELLING_PRICE to ' || t.owner || ': ' || SQLERRM);
            END;
        END IF;

        -- 2. Check and add SHOW_IN_POS
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner AND table_name = t.table_name AND column_name = 'SHOW_IN_POS';

        IF v_col_count = 0 THEN
            v_sql := 'ALTER TABLE "' || t.owner || '"."' || t.table_name || '" ADD ("SHOW_IN_POS" NUMBER(1) DEFAULT 1 NOT NULL)';
            BEGIN
                EXECUTE IMMEDIATE v_sql;
                DBMS_OUTPUT.PUT_LINE('Added SHOW_IN_POS to ' || t.owner || '.' || t.table_name);
            EXCEPTION
                WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('Error adding SHOW_IN_POS to ' || t.owner || ': ' || SQLERRM);
            END;
        END IF;

    END LOOP;
END;
/
COMMIT;
exit;
