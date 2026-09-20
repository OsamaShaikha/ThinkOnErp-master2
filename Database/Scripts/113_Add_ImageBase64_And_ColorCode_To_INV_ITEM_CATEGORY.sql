-- ==============================================================================
-- 113_Add_ImageBase64_And_ColorCode_To_INV_ITEM_CATEGORY.sql
-- Adds IMAGE_BASE64 (CLOB) and COLOR_CODE (NUMBER) columns to INV_ITEM_CATEGORY
-- in DEV_TEMPLATE and all tenant schemas
-- ==============================================================================

DECLARE
    v_sql VARCHAR2(1000);
    v_col_count NUMBER;
BEGIN
    FOR t IN (
        SELECT owner, table_name 
        FROM all_tables 
        WHERE table_name = 'INV_ITEM_CATEGORY' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        -- 1. Check & Add IMAGE_BASE64
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner AND table_name = t.table_name AND column_name = 'IMAGE_BASE64';

        IF v_col_count = 0 THEN
            v_sql := 'ALTER TABLE "' || t.owner || '"."' || t.table_name || '" ADD ("IMAGE_BASE64" CLOB NULL)';
            BEGIN
                EXECUTE IMMEDIATE v_sql;
                DBMS_OUTPUT.PUT_LINE('Added IMAGE_BASE64 to ' || t.owner || '.' || t.table_name);
            EXCEPTION
                WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('Error adding IMAGE_BASE64 to ' || t.owner || ': ' || SQLERRM);
            END;
        END IF;

        -- 2. Check & Add COLOR_CODE
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner AND table_name = t.table_name AND column_name = 'COLOR_CODE';

        IF v_col_count = 0 THEN
            v_sql := 'ALTER TABLE "' || t.owner || '"."' || t.table_name || '" ADD ("COLOR_CODE" NUMBER(10) NULL)';
            BEGIN
                EXECUTE IMMEDIATE v_sql;
                DBMS_OUTPUT.PUT_LINE('Added COLOR_CODE to ' || t.owner || '.' || t.table_name);
            EXCEPTION
                WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('Error adding COLOR_CODE to ' || t.owner || ': ' || SQLERRM);
            END;
        END IF;

    END LOOP;
END;
/
COMMIT;
exit;
