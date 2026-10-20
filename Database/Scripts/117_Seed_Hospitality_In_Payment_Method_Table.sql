-- ==============================================================================
-- 117_Seed_Hospitality_In_Payment_Method_Table.sql
-- Seeds 'HOSPITALITY' payment method into PAYMENT_METHOD table across all schemas
-- ==============================================================================

DECLARE
    v_cnt NUMBER;
BEGIN
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'PAYMENT_METHOD' 
          AND (owner = 'DEV_TEMPLATE' OR owner = 'THINKON_ERP' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE '
            MERGE INTO "' || t.owner || '"."PAYMENT_METHOD" dst
            USING (
                SELECT b."Id" AS branch_id, ''HOSPITALITY'' AS code
                FROM "' || t.owner || '"."SYS_BRANCH" b
                WHERE b."IS_ACTIVE" = 1
            ) src
            ON (dst."BRANCH_ID" = src.branch_id AND dst."CODE" = src.code)
            WHEN MATCHED THEN
                UPDATE SET 
                    dst."NAME_LOCAL" = N''ضيافة'',
                    dst."NAME_EN" = ''Hospitality'',
                    dst."METHOD_TYPE" = ''HOSPITALITY'',
                    dst."GL_ACCOUNT_CODE" = ''632102'',
                    dst."AUTO_POST_GL" = 1,
                    dst."SHOW_IN_POS" = 1,
                    dst."SHOW_IN_INVOICES" = 1,
                    dst."SHOW_IN_VOUCHERS" = 1,
                    dst."IS_ACTIVE" = 1,
                    dst."DISPLAY_ORDER" = 98,
                    dst."UPDATE_USER" = ''SYSTEM'',
                    dst."UPDATE_DATE" = SYSTIMESTAMP
            WHEN NOT MATCHED THEN
                INSERT (
                    "BRANCH_ID", "CODE", "NAME_LOCAL", "NAME_EN", "METHOD_TYPE", "GL_ACCOUNT_CODE",
                    "COMMISSION_PERCENT", "COMMISSION_FIXED_AMOUNT", "COMMISSION_GL_ACCOUNT_CODE",
                    "REQUIRES_REFERENCE", "REQUIRES_DUE_DATE", "AUTO_POST_GL",
                    "SHOW_IN_POS", "SHOW_IN_INVOICES", "SHOW_IN_VOUCHERS",
                    "IS_ACTIVE", "DISPLAY_ORDER", "CREATION_USER", "CREATION_DATE"
                )
                VALUES (
                    src.branch_id, src.code, N''ضيافة'', ''Hospitality'', ''HOSPITALITY'', ''632102'',
                    0, 0, NULL,
                    0, 0, 1,
                    1, 1, 1,
                    1, 98, ''SYSTEM'', SYSTIMESTAMP
                )';

            -- CUSTOMER_ACCOUNT
            EXECUTE IMMEDIATE '
            MERGE INTO "' || t.owner || '"."PAYMENT_METHOD" dst
            USING (
                SELECT b."Id" AS branch_id, ''CUSTOMER_ACCOUNT'' AS code
                FROM "' || t.owner || '"."SYS_BRANCH" b
                WHERE b."IS_ACTIVE" = 1
            ) src
            ON (dst."BRANCH_ID" = src.branch_id AND dst."CODE" = src.code)
            WHEN MATCHED THEN
                UPDATE SET 
                    dst."NAME_LOCAL" = N''حساب عميل (آجل)'',
                    dst."NAME_EN" = ''Customer Account'',
                    dst."METHOD_TYPE" = ''ACCOUNT'',
                    dst."GL_ACCOUNT_CODE" = ''112101'',
                    dst."AUTO_POST_GL" = 1,
                    dst."SHOW_IN_POS" = 1,
                    dst."SHOW_IN_INVOICES" = 1,
                    dst."SHOW_IN_VOUCHERS" = 1,
                    dst."IS_ACTIVE" = 1,
                    dst."DISPLAY_ORDER" = 99,
                    dst."UPDATE_USER" = ''SYSTEM'',
                    dst."UPDATE_DATE" = SYSTIMESTAMP
            WHEN NOT MATCHED THEN
                INSERT (
                    "BRANCH_ID", "CODE", "NAME_LOCAL", "NAME_EN", "METHOD_TYPE", "GL_ACCOUNT_CODE",
                    "COMMISSION_PERCENT", "COMMISSION_FIXED_AMOUNT", "COMMISSION_GL_ACCOUNT_CODE",
                    "REQUIRES_REFERENCE", "REQUIRES_DUE_DATE", "AUTO_POST_GL",
                    "SHOW_IN_POS", "SHOW_IN_INVOICES", "SHOW_IN_VOUCHERS",
                    "IS_ACTIVE", "DISPLAY_ORDER", "CREATION_USER", "CREATION_DATE"
                )
                VALUES (
                    src.branch_id, src.code, N''حساب عميل (آجل)'', ''Customer Account'', ''ACCOUNT'', ''112101'',
                    0, 0, NULL,
                    0, 0, 1,
                    1, 1, 1,
                    1, 99, ''SYSTEM'', SYSTIMESTAMP
                )';
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('Error on schema ' || t.owner || ': ' || SQLERRM);
        END;
    END LOOP;
END;
/
COMMIT;
exit;
