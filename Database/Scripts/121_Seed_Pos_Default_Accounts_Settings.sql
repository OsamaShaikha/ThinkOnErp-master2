-- ==============================================================================
-- 121_Seed_Pos_Default_Accounts_Settings.sql
-- Seeds POS Default GL Accounts into SYS_SETTINGS:
--   53 = POS Default Cash/Till Account ('111102')
--   54 = POS Default Expense Account ('525101')
--   55 = POS Default Receipt Account ('421101')
-- ==============================================================================

DECLARE
    v_cnt NUMBER;
BEGIN
    FOR s IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'SYS_SETTINGS' 
          AND (owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%')
        ORDER BY owner
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE 'MERGE INTO "' || s.owner || '"."SYS_SETTINGS" dst
            USING (
                SELECT 53 as code, N''POS Default Cash/Till Account (صندوق نقطة البيع)'' as descr, ''111102'' as val FROM dual UNION ALL
                SELECT 54 as code, N''POS Default Expense Account (مصاريف ونثريات نقطة البيع)'' as descr, ''525101'' as val FROM dual UNION ALL
                SELECT 55 as code, N''POS Default Receipt Account (إيرادات ومقبوضات نقطة البيع)'' as descr, ''421101'' as val FROM dual
            ) src
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
