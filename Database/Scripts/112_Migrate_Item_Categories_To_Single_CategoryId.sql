-- ==============================================================================
-- 112_Migrate_Item_Categories_To_Single_CategoryId.sql
-- Migrates MAIN_CATEGORY_ID and SUB_CATEGORY_ID to a single CATEGORY_ID column
-- in DEV_TEMPLATE and all tenant schemas
-- ==============================================================================

DECLARE
    v_sql VARCHAR2(1000);
    v_col_count NUMBER;
BEGIN
    -- 1. Drop all Foreign Keys on SUB_CATEGORY_ID
    FOR fk IN (
        SELECT a.owner, a.table_name, a.constraint_name 
        FROM all_cons_columns a 
        JOIN all_constraints c ON a.owner = c.owner AND a.constraint_name = c.constraint_name 
        WHERE a.table_name = 'INV_ITEM' 
          AND a.column_name = 'SUB_CATEGORY_ID' 
          AND c.constraint_type = 'R'
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || fk.owner || '"."' || fk.table_name || '" DROP CONSTRAINT "' || fk.constraint_name || '"';
            DBMS_OUTPUT.PUT_LINE('Dropped FK: ' || fk.owner || '.' || fk.constraint_name);
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('Error dropping FK ' || fk.constraint_name || ': ' || SQLERRM);
        END;
    END LOOP;

    -- 2. Drop composite indexes that reference SUB_CATEGORY_ID
    FOR idx IN (
        SELECT DISTINCT index_owner, index_name, table_owner, table_name
        FROM all_ind_columns
        WHERE table_name = 'INV_ITEM' AND column_name = 'SUB_CATEGORY_ID'
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE 'DROP INDEX "' || idx.index_owner || '"."' || idx.index_name || '"';
            DBMS_OUTPUT.PUT_LINE('Dropped composite index: ' || idx.index_owner || '.' || idx.index_name);
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('Error dropping index ' || idx.index_name || ': ' || SQLERRM);
        END;
    END LOOP;

    -- 3. For each tenant schema and DEV_TEMPLATE
    FOR t IN (
        SELECT owner, table_name 
        FROM all_tables 
        WHERE table_name = 'INV_ITEM' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        -- Check if SUB_CATEGORY_ID exists
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner AND table_name = t.table_name AND column_name = 'SUB_CATEGORY_ID';

        IF v_col_count > 0 THEN
            -- Update MAIN_CATEGORY_ID = SUB_CATEGORY_ID where SUB_CATEGORY_ID is not null
            v_sql := 'UPDATE "' || t.owner || '"."' || t.table_name || '" SET "MAIN_CATEGORY_ID" = "SUB_CATEGORY_ID" WHERE "SUB_CATEGORY_ID" IS NOT NULL';
            BEGIN
                EXECUTE IMMEDIATE v_sql;
            EXCEPTION
                WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('Error updating category for ' || t.owner || ': ' || SQLERRM);
            END;

            -- Drop column SUB_CATEGORY_ID
            v_sql := 'ALTER TABLE "' || t.owner || '"."' || t.table_name || '" DROP COLUMN "SUB_CATEGORY_ID"';
            BEGIN
                EXECUTE IMMEDIATE v_sql;
                DBMS_OUTPUT.PUT_LINE('Dropped SUB_CATEGORY_ID in ' || t.owner);
            EXCEPTION
                WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('Error dropping column in ' || t.owner || ': ' || SQLERRM);
            END;
        END IF;

        -- Check if MAIN_CATEGORY_ID exists, rename to CATEGORY_ID
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner AND table_name = t.table_name AND column_name = 'MAIN_CATEGORY_ID';

        IF v_col_count > 0 THEN
            v_sql := 'ALTER TABLE "' || t.owner || '"."' || t.table_name || '" RENAME COLUMN "MAIN_CATEGORY_ID" TO "CATEGORY_ID"';
            BEGIN
                EXECUTE IMMEDIATE v_sql;
                DBMS_OUTPUT.PUT_LINE('Renamed MAIN_CATEGORY_ID to CATEGORY_ID in ' || t.owner);
            EXCEPTION
                WHEN OTHERS THEN
                    DBMS_OUTPUT.PUT_LINE('Error renaming column in ' || t.owner || ': ' || SQLERRM);
            END;
        END IF;

        -- Create index on CATEGORY_ID if not exists
        SELECT COUNT(*) INTO v_col_count
        FROM all_ind_columns
        WHERE table_owner = t.owner AND table_name = t.table_name AND column_name = 'CATEGORY_ID';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'CREATE INDEX "' || t.owner || '"."IX_' || SUBSTR(REPLACE(t.owner, 'THINKONERP_', ''), 1, 8) || '_ITM_CAT" ON "' || t.owner || '"."' || t.table_name || '"("CATEGORY_ID")';
            EXCEPTION
                WHEN OTHERS THEN
                    NULL;
            END;
        END IF;

    END LOOP;
END;
/
COMMIT;
exit;
