-- ==============================================================================
-- 115_Seed_Pos_Customer_Settlement_Types_SysCode.sql
-- Seeds POS Customer Debt Settlement Types (CODE_MGR = 39) into SYS_CODE
-- ==============================================================================

DECLARE
    v_cnt NUMBER;
BEGIN
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'SYS_CODE' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        -- 0: Definition
        EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
        USING (SELECT 39 as mgr, 0 as mnr, 1 as lang, N''أنواع سداد الذمم في نقاط البيع'' as descr, ''POS_SETTLEMENT_TYPES'' as val FROM dual) src
        ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
        WHEN MATCHED THEN
          UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1
        WHEN NOT MATCHED THEN
          INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
          VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';

        -- 1: OnAccount, 2: SpecificInvoice, 3: AllInvoices
        EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
        USING (
            -- 1: OnAccount (دفعة على الحساب)
            SELECT 39 as mgr, 1 as mnr, 1 as lang, N''دفعة على الحساب (توزيع تلقائي FIFO)'' as descr, ''OnAccount'' as val FROM dual UNION ALL
            SELECT 39 as mgr, 1 as mnr, 2 as lang, N''On Account (Auto FIFO)'' as descr, ''OnAccount'' as val FROM dual UNION ALL
            -- 2: SpecificInvoice (سداد فاتورة محددة)
            SELECT 39 as mgr, 2 as mnr, 1 as lang, N''سداد فاتورة محددة'' as descr, ''SpecificInvoice'' as val FROM dual UNION ALL
            SELECT 39 as mgr, 2 as mnr, 2 as lang, N''Specific Invoice'' as descr, ''SpecificInvoice'' as val FROM dual UNION ALL
            -- 3: AllInvoices (سداد كامل الفواتير المفتوحة)
            SELECT 39 as mgr, 3 as mnr, 1 as lang, N''سداد كامل الفواتير المفتوحة'' as descr, ''AllInvoices'' as val FROM dual UNION ALL
            SELECT 39 as mgr, 3 as mnr, 2 as lang, N''All Open Invoices'' as descr, ''AllInvoices'' as val FROM dual
        ) src
        ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
        WHEN MATCHED THEN
          UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1
        WHEN NOT MATCHED THEN
          INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
          VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';

    END LOOP;
END;
/
COMMIT;
exit;
