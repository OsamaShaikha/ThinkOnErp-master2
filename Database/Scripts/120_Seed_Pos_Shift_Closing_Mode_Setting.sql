-- ==============================================================================
-- 120_Seed_Pos_Shift_Closing_Mode_Setting.sql
-- Seeds POS Shift Closing Modes (CODE_MGR = 40) into SYS_CODE
-- Seeds POS Shift Closing Mode Setting (SETTING_CODE = 52) into SYS_SETTINGS
-- ==============================================================================

DECLARE
    v_cnt NUMBER;
BEGIN
    -- Grant quota to tenant schemas if missing
    FOR u IN (SELECT username FROM all_users WHERE username LIKE 'THINKONERP_%' OR username = 'DEV_TEMPLATE') LOOP
        BEGIN
            EXECUTE IMMEDIATE 'ALTER USER "' || u.username || '" QUOTA UNLIMITED ON USERS';
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
    END LOOP;

    -- 1. SYS_CODE (Mgr = 40)
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'SYS_CODE' 
          AND (owner = 'THINKON_ERP' OR owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        BEGIN
            -- Definition (0)
            EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
            USING (SELECT 40 as mgr, 0 as mnr, 1 as lang, N''أوضاع إغلاق الوردية في نقاط البيع'' as descr, ''POS_SHIFT_CLOSING_MODES'' as val FROM dual) src
            ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
            WHEN MATCHED THEN
              UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1
            WHEN NOT MATCHED THEN
              INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
              VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';

            -- 1: EnforceBlindClose & 2: StandardOpenClose
            EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
            USING (
                SELECT 40 as mgr, 1 as mnr, 1 as lang, N''إغلاق أعمى إلزامي (حجب المتوقع)'' as descr, ''EnforceBlindClose'' as val FROM dual UNION ALL
                SELECT 40 as mgr, 1 as mnr, 2 as lang, N''Enforce Blind Close'' as descr, ''EnforceBlindClose'' as val FROM dual UNION ALL
                SELECT 40 as mgr, 2 as mnr, 1 as lang, N''إغلاق عادي مفتوح (كشف المتوقع)'' as descr, ''StandardOpenClose'' as val FROM dual UNION ALL
                SELECT 40 as mgr, 2 as mnr, 2 as lang, N''Standard Open Close'' as descr, ''StandardOpenClose'' as val FROM dual
            ) src
            ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
            WHEN MATCHED THEN
              UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1
            WHEN NOT MATCHED THEN
              INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
              VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
    END LOOP;

    -- 2. SYS_SETTINGS (Setting Code = 52)
    FOR s IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'SYS_SETTINGS' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%')
        ORDER BY owner
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE 'MERGE INTO "' || s.owner || '"."SYS_SETTINGS" dst
            USING (SELECT 52 as code, N''POS Shift Closing Mode (1: EnforceBlindClose, 2: StandardOpenClose)'' as descr, ''1'' as val FROM dual) src
            ON (dst.SETTING_CODE = src.code)
            WHEN MATCHED THEN
              UPDATE SET dst.SETTING_DESC = src.descr
            WHEN NOT MATCHED THEN
              INSERT (SETTING_CODE, SETTING_DESC, SETTING_VALUE)
              VALUES (src.code, src.descr, src.val)';
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
    END LOOP;

    COMMIT;
END;
/
