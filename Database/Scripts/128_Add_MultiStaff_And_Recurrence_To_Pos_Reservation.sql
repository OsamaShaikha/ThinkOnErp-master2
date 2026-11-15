-- ==============================================================================
-- 128_Add_MultiStaff_And_Recurrence_To_Pos_Reservation.sql
-- Adds ADDITIONAL_STAFF_IDS, RECURRENCE_GROUP_ID, and IS_RECURRING to POS_RESERVATION
-- Supports multi-staff salon services and recurring standing reservations
-- ==============================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;
SET FEEDBACK OFF;

DECLARE
    v_col_count NUMBER;
    v_schemas_processed NUMBER := 0;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting migration for POS_RESERVATION columns...');

    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'POS_RESERVATION' 
          AND (owner = 'THINKON_ERP' OR owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        DBMS_OUTPUT.PUT_LINE('>>> Schema: ' || t.owner);
        v_schemas_processed := v_schemas_processed + 1;

        -- 1. Check and add ADDITIONAL_STAFF_IDS column
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner 
          AND table_name = 'POS_RESERVATION' 
          AND column_name = 'ADDITIONAL_STAFF_IDS';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION" ADD ("ADDITIONAL_STAFF_IDS" NVARCHAR2(200) NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Added ADDITIONAL_STAFF_IDS');
            EXCEPTION WHEN OTHERS THEN 
                DBMS_OUTPUT.PUT_LINE('    ! Error adding ADDITIONAL_STAFF_IDS: ' || SQLERRM);
            END;
        ELSE
            DBMS_OUTPUT.PUT_LINE('    = ADDITIONAL_STAFF_IDS already exists');
        END IF;

        -- 2. Check and add RECURRENCE_GROUP_ID column
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner 
          AND table_name = 'POS_RESERVATION' 
          AND column_name = 'RECURRENCE_GROUP_ID';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION" ADD ("RECURRENCE_GROUP_ID" NVARCHAR2(100) NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Added RECURRENCE_GROUP_ID');
            EXCEPTION WHEN OTHERS THEN 
                DBMS_OUTPUT.PUT_LINE('    ! Error adding RECURRENCE_GROUP_ID: ' || SQLERRM);
            END;
        ELSE
            DBMS_OUTPUT.PUT_LINE('    = RECURRENCE_GROUP_ID already exists');
        END IF;

        -- 3. Check and add IS_RECURRING column
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_cols
        WHERE owner = t.owner 
          AND table_name = 'POS_RESERVATION' 
          AND column_name = 'IS_RECURRING';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION" ADD ("IS_RECURRING" NUMBER(1) DEFAULT 0 NOT NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Added IS_RECURRING');
            EXCEPTION WHEN OTHERS THEN 
                DBMS_OUTPUT.PUT_LINE('    ! Error adding IS_RECURRING: ' || SQLERRM);
            END;
        ELSE
            DBMS_OUTPUT.PUT_LINE('    = IS_RECURRING already exists');
        END IF;

        -- 4. Create Index on RECURRENCE_GROUP_ID for series lookups and fast cancellation
        BEGIN
            EXECUTE IMMEDIATE 'CREATE INDEX "' || t.owner || '"."IX_POS_RES_RECURRENCE" ON "' || t.owner || '"."POS_RESERVATION" ("RECURRENCE_GROUP_ID")';
            DBMS_OUTPUT.PUT_LINE('    + Created index IX_POS_RES_RECURRENCE');
        EXCEPTION 
            WHEN OTHERS THEN 
                -- If index already exists (ORA-00955 or ORA-01408), ignore or log
                DBMS_OUTPUT.PUT_LINE('    = Index IX_POS_RES_RECURRENCE note: ' || SQLERRM);
        END;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('---------------------------------------------');
    DBMS_OUTPUT.PUT_LINE('Completed successfully! Total schemas processed: ' || v_schemas_processed);
END;
/

EXIT;
