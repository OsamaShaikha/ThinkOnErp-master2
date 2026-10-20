-- ==============================================================================
-- 121_Add_Parent_Table_Id_To_Pos_Table.sql
-- Adds PARENT_TABLE_ID to POS_TABLE to support Table Merging / Combining
-- ==============================================================================

DECLARE
    v_col_count NUMBER;
BEGIN
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'POS_TABLE' 
          AND (owner = 'THINKON_ERP' OR owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        -- 1. Check and add PARENT_TABLE_ID column
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner 
          AND table_name = 'POS_TABLE' 
          AND column_name = 'PARENT_TABLE_ID';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_TABLE" ADD ("PARENT_TABLE_ID" NUMBER(19))';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
        END IF;

        -- 2. Add foreign key FK_POS_TABLE_PARENT_TABLE_ID if not exists
        BEGIN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_TABLE" ADD CONSTRAINT "FK_POS_TABLE_PARENT_TABLE" FOREIGN KEY ("PARENT_TABLE_ID") REFERENCES "' || t.owner || '"."POS_TABLE" ("ID") ON DELETE SET NULL';
        EXCEPTION WHEN OTHERS THEN NULL;
        END;

        -- 3. Add Index IX_POS_TABLE_PARENT_TABLE_ID
        BEGIN
            EXECUTE IMMEDIATE 'CREATE INDEX "' || t.owner || '"."IX_POS_TABLE_PARENT_TABLE" ON "' || t.owner || '"."POS_TABLE" ("PARENT_TABLE_ID")';
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
    END LOOP;
END;
/
