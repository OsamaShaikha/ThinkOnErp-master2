-- ==============================================================================
-- 130_Update_Pos_Reservation_Service_Staff_Columns.sql
-- Updates POS_RESERVATION_SERVICE table to support multiple primary staff
-- (PRIMARY_STAFF_IDS) and multiple assistant staff (ASSISTANT_STAFF_IDS).
-- ==============================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;
SET FEEDBACK OFF;
SET DEFINE OFF;

DECLARE
    v_col_count NUMBER;
    v_schemas_processed NUMBER := 0;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting migration for POS_RESERVATION_SERVICE staff columns...');

    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'POS_RESERVATION_SERVICE' 
          AND (owner = 'THINKON_ERP' OR owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        DBMS_OUTPUT.PUT_LINE('>>> Schema: ' || t.owner);
        v_schemas_processed := v_schemas_processed + 1;

        -- 1. Check and Add PRIMARY_STAFF_IDS
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_columns
        WHERE owner = t.owner AND table_name = 'POS_RESERVATION_SERVICE' AND column_name = 'PRIMARY_STAFF_IDS';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION_SERVICE" ADD ("PRIMARY_STAFF_IDS" NVARCHAR2(200) NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Added column PRIMARY_STAFF_IDS');
            EXCEPTION WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('    ! Error adding PRIMARY_STAFF_IDS: ' || SQLERRM);
            END;
        ELSE
            DBMS_OUTPUT.PUT_LINE('    = Column PRIMARY_STAFF_IDS already exists');
        END IF;

        -- 2. Check and Add ASSISTANT_STAFF_IDS
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_columns
        WHERE owner = t.owner AND table_name = 'POS_RESERVATION_SERVICE' AND column_name = 'ASSISTANT_STAFF_IDS';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION_SERVICE" ADD ("ASSISTANT_STAFF_IDS" NVARCHAR2(200) NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Added column ASSISTANT_STAFF_IDS');
            EXCEPTION WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('    ! Error adding ASSISTANT_STAFF_IDS: ' || SQLERRM);
            END;
        ELSE
            DBMS_OUTPUT.PUT_LINE('    = Column ASSISTANT_STAFF_IDS already exists');
        END IF;

        -- 3. Migrate data from PRIMARY_STAFF_ID if present
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_columns
        WHERE owner = t.owner AND table_name = 'POS_RESERVATION_SERVICE' AND column_name = 'PRIMARY_STAFF_ID';

        IF v_col_count > 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'UPDATE "' || t.owner || '"."POS_RESERVATION_SERVICE" SET "PRIMARY_STAFF_IDS" = TO_CHAR("PRIMARY_STAFF_ID") WHERE "PRIMARY_STAFF_ID" IS NOT NULL AND "PRIMARY_STAFF_IDS" IS NULL';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
        END IF;

        -- 4. Migrate data from ADDITIONAL_STAFF_IDS if present
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_columns
        WHERE owner = t.owner AND table_name = 'POS_RESERVATION_SERVICE' AND column_name = 'ADDITIONAL_STAFF_IDS';

        IF v_col_count > 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'UPDATE "' || t.owner || '"."POS_RESERVATION_SERVICE" SET "ASSISTANT_STAFF_IDS" = "ADDITIONAL_STAFF_IDS" WHERE "ADDITIONAL_STAFF_IDS" IS NOT NULL AND "ASSISTANT_STAFF_IDS" IS NULL';
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
