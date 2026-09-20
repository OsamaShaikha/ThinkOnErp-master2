-- ==============================================================================
-- 114_Seed_Pos_Order_Types_SysCode.sql
-- Seeds POS Order Types (CODE_MGR = 34) into SYS_CODE for all tenant schemas
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
        -- Definition Header (mnr = 0)
        SELECT COUNT(*) INTO v_cnt FROM all_tab_cols WHERE owner = t.owner AND table_name = 'SYS_CODE';
        
        -- Insert Arabic and English records for each order type
        -- 0: Definition
        MERGE INTO "SYS_CODE" dst
        USING (SELECT 34 as mgr, 0 as mnr, 1 as lang, N'أنواع طلبات نقاط البيع' as descr, 'POS_ORDER_TYPES' as val FROM dual) src
        ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
        WHEN NOT MATCHED THEN
          INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
          VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, 'SYSTEM', SYSTIMESTAMP);

        -- 1: DineIn (محلي / صالة)
        EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
        USING (
            SELECT 34 as mgr, 1 as mnr, 1 as lang, N''محلي / صالة'' as descr, ''DineIn'' as val FROM dual UNION ALL
            SELECT 34 as mgr, 1 as mnr, 2 as lang, N''Dine In'' as descr, ''DineIn'' as val FROM dual UNION ALL
            -- 2: Takeaway (سفري / استلام)
            SELECT 34 as mgr, 2 as mnr, 1 as lang, N''سفري / استلام'' as descr, ''Takeaway'' as val FROM dual UNION ALL
            SELECT 34 as mgr, 2 as mnr, 2 as lang, N''Takeaway'' as descr, ''Takeaway'' as val FROM dual UNION ALL
            -- 3: Delivery (توصيل)
            SELECT 34 as mgr, 3 as mnr, 1 as lang, N''توصيل'' as descr, ''Delivery'' as val FROM dual UNION ALL
            SELECT 34 as mgr, 3 as mnr, 2 as lang, N''Delivery'' as descr, ''Delivery'' as val FROM dual UNION ALL
            -- 4: Aggregator (تطبيقات التوصيل)
            SELECT 34 as mgr, 4 as mnr, 1 as lang, N''تطبيقات التوصيل'' as descr, ''Aggregator'' as val FROM dual UNION ALL
            SELECT 34 as mgr, 4 as mnr, 2 as lang, N''Aggregator'' as descr, ''Aggregator'' as val FROM dual UNION ALL
            -- 5: Kiosk (خدمة ذاتية)
            SELECT 34 as mgr, 5 as mnr, 1 as lang, N''خدمة ذاتية (كشك)'' as descr, ''Kiosk'' as val FROM dual UNION ALL
            SELECT 34 as mgr, 5 as mnr, 2 as lang, N''Self-Service Kiosk'' as descr, ''Kiosk'' as val FROM dual UNION ALL
            -- 6: QrTable (طلب عبر الطاولة QR)
            SELECT 34 as mgr, 6 as mnr, 1 as lang, N''طلب عبر الطاولة (QR)'' as descr, ''QrTable'' as val FROM dual UNION ALL
            SELECT 34 as mgr, 6 as mnr, 2 as lang, N''QR Table Order'' as descr, ''QrTable'' as val FROM dual
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
