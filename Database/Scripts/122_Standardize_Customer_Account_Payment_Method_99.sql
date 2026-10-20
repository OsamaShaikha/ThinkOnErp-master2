-- ==============================================================================
-- 122_Standardize_Customer_Account_99_And_Hospitality_98.sql
-- Standardizes Customer Account Payment Method to ID 99 and Hospitality to ID 98 across all schemas.
-- ==============================================================================

DECLARE
    v_has99 NUMBER;
    v_has98 NUMBER;
BEGIN
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'PAYMENT_METHOD' 
          AND (owner = 'DEV_TEMPLATE' OR owner = 'THINKON_ERP' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        BEGIN
            -- 1. Standardize SYS_CODE (CODE_MGR = 37)
            EXECUTE IMMEDIATE '
            MERGE INTO "' || t.owner || '"."SYS_CODE" dst
            USING (
                SELECT 37 as mgr, 99 as mnr, 1 as lang, N''حساب عميل (آجل)'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
                SELECT 37 as mgr, 99 as mnr, 2 as lang, N''Customer Account'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
                SELECT 37 as mgr, 98 as mnr, 1 as lang, N''ضيافة'' as descr, ''Hospitality'' as val FROM dual UNION ALL
                SELECT 37 as mgr, 98 as mnr, 2 as lang, N''Hospitality'' as descr, ''Hospitality'' as val FROM dual UNION ALL
                SELECT 37 as mgr, 4 as mnr, 1 as lang, N''حساب عميل (آجل) (قديم)'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
                SELECT 37 as mgr, 4 as mnr, 2 as lang, N''Customer Account (Legacy)'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
                SELECT 37 as mgr, 11 as mnr, 1 as lang, N''ضيافة (قديم)'' as descr, ''Hospitality'' as val FROM dual UNION ALL
                SELECT 37 as mgr, 11 as mnr, 2 as lang, N''Hospitality (Legacy)'' as descr, ''Hospitality'' as val FROM dual
            ) src
            ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
            WHEN MATCHED THEN
              UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1
            WHEN NOT MATCHED THEN
              INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
              VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';

            -- 2. Update PAYMENT_METHOD table ID to 99 for Customer Account
            EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "' || t.owner || '"."PAYMENT_METHOD" WHERE "ID" = 99' INTO v_has99;
            IF v_has99 = 0 THEN
                EXECUTE IMMEDIATE '
                UPDATE "' || t.owner || '"."PAYMENT_METHOD"
                SET "ID" = 99, "DISPLAY_ORDER" = 99
                WHERE "BRANCH_ID" = 1 AND "CODE" = ''CUSTOMER_ACCOUNT''';
            ELSE
                EXECUTE IMMEDIATE '
                UPDATE "' || t.owner || '"."PAYMENT_METHOD"
                SET "CODE" = ''CUSTOMER_ACCOUNT'', "NAME_LOCAL" = N''حساب عميل (آجل)'', "NAME_EN" = ''Customer Account'',
                    "METHOD_TYPE" = ''ACCOUNT'', "GL_ACCOUNT_CODE" = ''112101'', "DISPLAY_ORDER" = 99, "IS_ACTIVE" = 1
                WHERE "ID" = 99';
            END IF;

            -- 3. Update PAYMENT_METHOD table ID to 98 for Hospitality
            EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "' || t.owner || '"."PAYMENT_METHOD" WHERE "ID" = 98' INTO v_has98;
            IF v_has98 = 0 THEN
                EXECUTE IMMEDIATE '
                UPDATE "' || t.owner || '"."PAYMENT_METHOD"
                SET "ID" = 98, "DISPLAY_ORDER" = 98
                WHERE "BRANCH_ID" = 1 AND "CODE" = ''HOSPITALITY''';
            ELSE
                EXECUTE IMMEDIATE '
                UPDATE "' || t.owner || '"."PAYMENT_METHOD"
                SET "CODE" = ''HOSPITALITY'', "NAME_LOCAL" = N''ضيافة'', "NAME_EN" = ''Hospitality'',
                    "METHOD_TYPE" = ''HOSPITALITY'', "GL_ACCOUNT_CODE" = ''632102'', "DISPLAY_ORDER" = 98, "IS_ACTIVE" = 1
                WHERE "ID" = 98';
            END IF;

            -- 4. Migrate historical POS_ORDER_PAYMENT records to 99 and 98
            BEGIN
                EXECUTE IMMEDIATE '
                UPDATE "' || t.owner || '"."POS_ORDER_PAYMENT"
                SET "PAYMENT_METHOD" = 99
                WHERE "PAYMENT_METHOD" IN (4, 43)';
            EXCEPTION
                WHEN OTHERS THEN NULL;
            END;

            BEGIN
                EXECUTE IMMEDIATE '
                UPDATE "' || t.owner || '"."POS_ORDER_PAYMENT"
                SET "PAYMENT_METHOD" = 98
                WHERE "PAYMENT_METHOD" IN (11, 41)';
            EXCEPTION
                WHEN OTHERS THEN NULL;
            END;

        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('Error on schema ' || t.owner || ': ' || SQLERRM);
        END;
    END LOOP;
END;
/
COMMIT;
exit;
