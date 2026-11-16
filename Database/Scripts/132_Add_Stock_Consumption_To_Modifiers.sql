-- ==============================================================================
-- 132_Add_Stock_Consumption_To_Modifiers.sql
-- Adds stock consumption fields to INV_MODIFIER_OPTION and POS_ORDER_LINE_MODIFIER
-- across all tenant and development schemas.
-- ==============================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;
SET FEEDBACK OFF;
SET DEFINE OFF;

DECLARE
    v_col_count NUMBER;
    v_target_table VARCHAR2(50);
    v_schemas_processed NUMBER := 0;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting migration for Modifier Stock Consumption...');

    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name IN ('INV_MODIFIER_OPTION', 'POS_MODIFIER_OPTION', 'POS_ORDER_LINE_MODIFIER')
          AND (owner = 'THINKON_ERP' OR owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        GROUP BY owner
        ORDER BY owner
    ) LOOP
        DBMS_OUTPUT.PUT_LINE('>>> Schema: ' || t.owner);
        v_schemas_processed := v_schemas_processed + 1;

        -- Determine whether table is named INV_MODIFIER_OPTION or POS_MODIFIER_OPTION
        SELECT COUNT(*) INTO v_col_count
        FROM all_tables
        WHERE owner = t.owner AND table_name = 'INV_MODIFIER_OPTION';

        IF v_col_count > 0 THEN
            v_target_table := 'INV_MODIFIER_OPTION';
        ELSE
            SELECT COUNT(*) INTO v_col_count
            FROM all_tables
            WHERE owner = t.owner AND table_name = 'POS_MODIFIER_OPTION';
            IF v_col_count > 0 THEN
                v_target_table := 'POS_MODIFIER_OPTION';
            ELSE
                v_target_table := NULL;
            END IF;
        END IF;

        -- 1. Updates on Modifier Option table
        IF v_target_table IS NOT NULL THEN
            -- RELATED_ITEM_QTY
            SELECT COUNT(*) INTO v_col_count FROM all_tab_columns
            WHERE owner = t.owner AND table_name = v_target_table AND column_name = 'RELATED_ITEM_QTY';
            IF v_col_count = 0 THEN
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."' || v_target_table || '" ADD ("RELATED_ITEM_QTY" NUMBER(18,4) DEFAULT 1.0 NOT NULL)';
                    DBMS_OUTPUT.PUT_LINE('    + Added RELATED_ITEM_QTY to ' || v_target_table);
                EXCEPTION WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('    ! Error adding RELATED_ITEM_QTY: ' || SQLERRM);
                END;
            END IF;

            -- RELATED_UOM_CODE
            SELECT COUNT(*) INTO v_col_count FROM all_tab_columns
            WHERE owner = t.owner AND table_name = v_target_table AND column_name = 'RELATED_UOM_CODE';
            IF v_col_count = 0 THEN
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."' || v_target_table || '" ADD ("RELATED_UOM_CODE" NVARCHAR2(20) NULL)';
                    DBMS_OUTPUT.PUT_LINE('    + Added RELATED_UOM_CODE to ' || v_target_table);
                EXCEPTION WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('    ! Error adding RELATED_UOM_CODE: ' || SQLERRM);
                END;
            END IF;

            -- UOM_FACTOR
            SELECT COUNT(*) INTO v_col_count FROM all_tab_columns
            WHERE owner = t.owner AND table_name = v_target_table AND column_name = 'UOM_FACTOR';
            IF v_col_count = 0 THEN
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."' || v_target_table || '" ADD ("UOM_FACTOR" NUMBER(18,6) DEFAULT 1.0 NOT NULL)';
                    DBMS_OUTPUT.PUT_LINE('    + Added UOM_FACTOR to ' || v_target_table);
                EXCEPTION WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('    ! Error adding UOM_FACTOR: ' || SQLERRM);
                END;
            END IF;

            -- OPTION_TYPE
            SELECT COUNT(*) INTO v_col_count FROM all_tab_columns
            WHERE owner = t.owner AND table_name = v_target_table AND column_name = 'OPTION_TYPE';
            IF v_col_count = 0 THEN
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."' || v_target_table || '" ADD ("OPTION_TYPE" NUMBER(10) DEFAULT 1 NOT NULL)';
                    DBMS_OUTPUT.PUT_LINE('    + Added OPTION_TYPE to ' || v_target_table);
                EXCEPTION WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('    ! Error adding OPTION_TYPE: ' || SQLERRM);
                END;
            END IF;

            -- REPLACES_ITEM_ID
            SELECT COUNT(*) INTO v_col_count FROM all_tab_columns
            WHERE owner = t.owner AND table_name = v_target_table AND column_name = 'REPLACES_ITEM_ID';
            IF v_col_count = 0 THEN
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."' || v_target_table || '" ADD ("REPLACES_ITEM_ID" NUMBER(19) NULL)';
                    DBMS_OUTPUT.PUT_LINE('    + Added REPLACES_ITEM_ID to ' || v_target_table);
                EXCEPTION WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('    ! Error adding REPLACES_ITEM_ID: ' || SQLERRM);
                END;
            END IF;

            -- Index on REPLACES_ITEM_ID
            BEGIN
                EXECUTE IMMEDIATE 'CREATE INDEX "' || t.owner || '"."IX_' || SUBSTR(v_target_table, 1, 10) || '_REP_ITM" ON "' || t.owner || '"."' || v_target_table || '" ("REPLACES_ITEM_ID")';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- FK on REPLACES_ITEM_ID to INV_ITEM
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."' || v_target_table || '" ADD CONSTRAINT "FK_' || SUBSTR(v_target_table, 1, 10) || '_REP_ITM" FOREIGN KEY ("REPLACES_ITEM_ID") REFERENCES "' || t.owner || '"."INV_ITEM" ("ID") ON DELETE SET NULL';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
        END IF;

        -- 2. Updates on POS_ORDER_LINE_MODIFIER
        SELECT COUNT(*) INTO v_col_count
        FROM all_tables
        WHERE owner = t.owner AND table_name = 'POS_ORDER_LINE_MODIFIER';

        IF v_col_count > 0 THEN
            -- MODIFIER_OPTION_ID
            SELECT COUNT(*) INTO v_col_count FROM all_tab_columns
            WHERE owner = t.owner AND table_name = 'POS_ORDER_LINE_MODIFIER' AND column_name = 'MODIFIER_OPTION_ID';
            IF v_col_count = 0 THEN
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_ORDER_LINE_MODIFIER" ADD ("MODIFIER_OPTION_ID" NUMBER(19) NULL)';
                    DBMS_OUTPUT.PUT_LINE('    + Added MODIFIER_OPTION_ID to POS_ORDER_LINE_MODIFIER');
                EXCEPTION WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('    ! Error adding MODIFIER_OPTION_ID: ' || SQLERRM);
                END;
            END IF;

            -- Make MODIFIER_ITEM_ID nullable
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_ORDER_LINE_MODIFIER" MODIFY ("MODIFIER_ITEM_ID" NUMBER(19) NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Made MODIFIER_ITEM_ID nullable in POS_ORDER_LINE_MODIFIER');
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- Index on MODIFIER_OPTION_ID
            BEGIN
                EXECUTE IMMEDIATE 'CREATE INDEX "' || t.owner || '"."IX_POS_ORD_LN_MOD_OPT_ID" ON "' || t.owner || '"."POS_ORDER_LINE_MODIFIER" ("MODIFIER_OPTION_ID")';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
        END IF;

        COMMIT;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('---------------------------------------------');
    DBMS_OUTPUT.PUT_LINE('Completed successfully! Total schemas processed: ' || v_schemas_processed);
END;
/

EXIT;
