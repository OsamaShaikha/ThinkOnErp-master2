-- ==============================================================================
-- 131_Add_Order_Columns_To_Pos_Reservation.sql
-- Adds ORDER_ID and ORDER_NUMBER columns to POS_RESERVATION table across all schemas.
-- ==============================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;
SET FEEDBACK OFF;
SET DEFINE OFF;

DECLARE
    v_col_count NUMBER;
    v_schemas_processed NUMBER := 0;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting migration for POS_RESERVATION order columns...');

    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'POS_RESERVATION' 
          AND (owner = 'THINKON_ERP' OR owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        DBMS_OUTPUT.PUT_LINE('>>> Schema: ' || t.owner);
        v_schemas_processed := v_schemas_processed + 1;

        -- 1. Check and Add ORDER_ID
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_columns
        WHERE owner = t.owner AND table_name = 'POS_RESERVATION' AND column_name = 'ORDER_ID';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION" ADD ("ORDER_ID" NUMBER(19) NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Added column ORDER_ID');
            EXCEPTION WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('    ! Error adding ORDER_ID: ' || SQLERRM);
            END;
        ELSE
            DBMS_OUTPUT.PUT_LINE('    = Column ORDER_ID already exists');
        END IF;

        -- 2. Check and Add ORDER_NUMBER
        SELECT COUNT(*) INTO v_col_count
        FROM all_tab_columns
        WHERE owner = t.owner AND table_name = 'POS_RESERVATION' AND column_name = 'ORDER_NUMBER';

        IF v_col_count = 0 THEN
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION" ADD ("ORDER_NUMBER" NVARCHAR2(50) NULL)';
                DBMS_OUTPUT.PUT_LINE('    + Added column ORDER_NUMBER');
            EXCEPTION WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('    ! Error adding ORDER_NUMBER: ' || SQLERRM);
            END;
        ELSE
            DBMS_OUTPUT.PUT_LINE('    = Column ORDER_NUMBER already exists');
        END IF;

        -- 3. Create Index on ORDER_ID
        BEGIN
            EXECUTE IMMEDIATE 'CREATE INDEX "' || t.owner || '"."IX_POS_RES_ORDER_ID" ON "' || t.owner || '"."POS_RESERVATION" ("ORDER_ID")';
            DBMS_OUTPUT.PUT_LINE('    + Created index IX_POS_RES_ORDER_ID');
        EXCEPTION WHEN OTHERS THEN NULL;
        END;

        -- 4. Add FK to POS_ORDER_HEADER if exists
        BEGIN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || t.owner || '"."POS_RESERVATION" ADD CONSTRAINT "FK_POS_RES_ORDER" FOREIGN KEY ("ORDER_ID") REFERENCES "' || t.owner || '"."POS_ORDER_HEADER" ("ID") ON DELETE SET NULL';
            DBMS_OUTPUT.PUT_LINE('    + Added foreign key FK_POS_RES_ORDER');
        EXCEPTION WHEN OTHERS THEN NULL;
        END;

        COMMIT;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('---------------------------------------------');
    DBMS_OUTPUT.PUT_LINE('Completed successfully! Total schemas processed: ' || v_schemas_processed);
END;
/

EXIT;
