-- ==============================================================================
-- 123_Seed_Warehouse_And_Zone_Types_SysCodes.sql
-- Seeds Warehouse Types (CODE_MGR = 22) and Zone Types (CODE_MGR = 23)
-- into SYS_CODE for all tenant schemas and template
-- ==============================================================================

DECLARE
BEGIN
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'SYS_CODE' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        -- Seed Warehouse Types (CODE_MGR = 22)
        EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
        USING (
            SELECT 22 as mgr, 0 as mnr, 1 as lang, N''أنواع المستودعات'' as descr, ''WAREHOUSE_TYPES'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 0 as mnr, 2 as lang, N''Warehouse Types'' as descr, ''WAREHOUSE_TYPES'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 1 as mnr, 1 as lang, N''مستودع رئيسي'' as descr, ''MAIN'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 1 as mnr, 2 as lang, N''Main Warehouse'' as descr, ''MAIN'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 2 as mnr, 1 as lang, N''مستودع فرعي'' as descr, ''BRANCH'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 2 as mnr, 2 as lang, N''Branch Warehouse'' as descr, ''BRANCH'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 3 as mnr, 1 as lang, N''مستودع ترانزيت / عبور'' as descr, ''TRANSIT'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 3 as mnr, 2 as lang, N''Transit Warehouse'' as descr, ''TRANSIT'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 4 as mnr, 1 as lang, N''مستودع افتراضي'' as descr, ''VIRTUAL'' as val FROM dual UNION ALL
            SELECT 22 as mgr, 4 as mnr, 2 as lang, N''Virtual Warehouse'' as descr, ''VIRTUAL'' as val FROM dual
        ) src
        ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
        WHEN MATCHED THEN
          UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1, dst.UPDATE_DATE = SYSTIMESTAMP
        WHEN NOT MATCHED THEN
          INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
          VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';

        -- Seed Zone Types (CODE_MGR = 23)
        EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
        USING (
            SELECT 23 as mgr, 0 as mnr, 1 as lang, N''أنواع مناطق المستودعات'' as descr, ''ZONE_TYPES'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 0 as mnr, 2 as lang, N''Warehouse Zone Types'' as descr, ''ZONE_TYPES'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 1 as mnr, 1 as lang, N''منطقة تخزين'' as descr, ''STORAGE'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 1 as mnr, 2 as lang, N''Storage Zone'' as descr, ''STORAGE'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 2 as mnr, 1 as lang, N''منطقة استلام'' as descr, ''RECEIVING'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 2 as mnr, 2 as lang, N''Receiving Zone'' as descr, ''RECEIVING'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 3 as mnr, 1 as lang, N''منطقة تجهيز / شحن'' as descr, ''STAGING'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 3 as mnr, 2 as lang, N''Staging Zone'' as descr, ''STAGING'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 4 as mnr, 1 as lang, N''منطقة حجر صحي / فحص'' as descr, ''QUARANTINE'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 4 as mnr, 2 as lang, N''Quarantine Zone'' as descr, ''QUARANTINE'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 5 as mnr, 1 as lang, N''منطقة بضاعة تالفة'' as descr, ''DAMAGED'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 5 as mnr, 2 as lang, N''Damaged Goods Zone'' as descr, ''DAMAGED'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 6 as mnr, 1 as lang, N''منطقة مرتجعات'' as descr, ''RETURNS'' as val FROM dual UNION ALL
            SELECT 23 as mgr, 6 as mnr, 2 as lang, N''Returns Zone'' as descr, ''RETURNS'' as val FROM dual
        ) src
        ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
        WHEN MATCHED THEN
          UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1, dst.UPDATE_DATE = SYSTIMESTAMP
        WHEN NOT MATCHED THEN
          INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
          VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';
    END LOOP;
    COMMIT;
END;
/
