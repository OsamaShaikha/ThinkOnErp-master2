-- ==============================================================================
-- 116_Seed_Pos_Payment_Methods_SysCode.sql
-- Seeds POS Payment Methods (CODE_MGR = 37) into SYS_CODE
-- ==============================================================================

DECLARE
    v_cnt NUMBER;
BEGIN
    FOR t IN (
        SELECT owner 
        FROM all_tables 
        WHERE table_name = 'SYS_CODE' 
          AND (owner = 'THINKON_ERP' OR owner = 'DEV_TEMPLATE' OR owner LIKE 'THINKONERP_%' OR owner = '1')
        ORDER BY owner
    ) LOOP
        -- 0: Definition
        EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
        USING (SELECT 37 as mgr, 0 as mnr, 1 as lang, N''طرق الدفع في نقاط البيع'' as descr, ''POS_PAYMENT_METHODS'' as val FROM dual) src
        ON (dst.CODE_MGR = src.mgr AND dst.CODE_MNR = src.mnr AND dst.CODE_LANG = src.lang)
        WHEN MATCHED THEN
          UPDATE SET dst.CODE_DESC = src.descr, dst.CODE_VALUE = src.val, dst.IS_ACTIVE = 1
        WHEN NOT MATCHED THEN
          INSERT (CODE_MGR, CODE_MNR, CODE_LANG, CODE_DESC, CODE_VALUE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
          VALUES (src.mgr, src.mnr, src.lang, src.descr, src.val, 1, ''SYSTEM'', SYSTIMESTAMP)';

        -- 1 to 11
        EXECUTE IMMEDIATE 'MERGE INTO "' || t.owner || '"."SYS_CODE" dst
        USING (
            -- 1: Cash
            SELECT 37 as mgr, 1 as mnr, 1 as lang, N''نقداً (كاش)'' as descr, ''Cash'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 1 as mnr, 2 as lang, N''Cash'' as descr, ''Cash'' as val FROM dual UNION ALL
            -- 2: Card
            SELECT 37 as mgr, 2 as mnr, 1 as lang, N''بطاقة دفع / مدى'' as descr, ''Card'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 2 as mnr, 2 as lang, N''Card'' as descr, ''Card'' as val FROM dual UNION ALL
            -- 3: Split
            SELECT 37 as mgr, 3 as mnr, 1 as lang, N''دفع مجزأ / متعدد'' as descr, ''Split'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 3 as mnr, 2 as lang, N''Split Payment'' as descr, ''Split'' as val FROM dual UNION ALL
            -- 99: CustomerAccount (Primary)
            SELECT 37 as mgr, 99 as mnr, 1 as lang, N''حساب عميل (آجل)'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 99 as mnr, 2 as lang, N''Customer Account'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
            -- 4: CustomerAccount (Legacy compatibility)
            SELECT 37 as mgr, 4 as mnr, 1 as lang, N''حساب عميل (آجل) (قديم)'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 4 as mnr, 2 as lang, N''Customer Account (Legacy)'' as descr, ''CustomerAccount'' as val FROM dual UNION ALL
            -- 5: Cheque
            SELECT 37 as mgr, 5 as mnr, 1 as lang, N''شيك'' as descr, ''Cheque'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 5 as mnr, 2 as lang, N''Cheque'' as descr, ''Cheque'' as val FROM dual UNION ALL
            -- 6: BankTransfer
            SELECT 37 as mgr, 6 as mnr, 1 as lang, N''تحويل بنكي'' as descr, ''BankTransfer'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 6 as mnr, 2 as lang, N''Bank Transfer'' as descr, ''BankTransfer'' as val FROM dual UNION ALL
            -- 7: DigitalWallet
            SELECT 37 as mgr, 7 as mnr, 1 as lang, N''محفظة رقمية'' as descr, ''DigitalWallet'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 7 as mnr, 2 as lang, N''Digital Wallet'' as descr, ''DigitalWallet'' as val FROM dual UNION ALL
            -- 8: LoyaltyPoints
            SELECT 37 as mgr, 8 as mnr, 1 as lang, N''نقاط ولاء'' as descr, ''LoyaltyPoints'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 8 as mnr, 2 as lang, N''Loyalty Points'' as descr, ''LoyaltyPoints'' as val FROM dual UNION ALL
            -- 9: GiftCard
            SELECT 37 as mgr, 9 as mnr, 1 as lang, N''بطاقة هدايا'' as descr, ''GiftCard'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 9 as mnr, 2 as lang, N''Gift Card'' as descr, ''GiftCard'' as val FROM dual UNION ALL
            -- 10: AggregatorPaid
            SELECT 37 as mgr, 10 as mnr, 1 as lang, N''مسدد عبر منصة التوصيل'' as descr, ''AggregatorPaid'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 10 as mnr, 2 as lang, N''Aggregator Paid'' as descr, ''AggregatorPaid'' as val FROM dual UNION ALL
            -- 98: Hospitality (Primary)
            SELECT 37 as mgr, 98 as mnr, 1 as lang, N''ضيافة'' as descr, ''Hospitality'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 98 as mnr, 2 as lang, N''Hospitality'' as descr, ''Hospitality'' as val FROM dual UNION ALL
            -- 11: Hospitality (Legacy compatibility)
            SELECT 37 as mgr, 11 as mnr, 1 as lang, N''ضيافة (قديم)'' as descr, ''Hospitality'' as val FROM dual UNION ALL
            SELECT 37 as mgr, 11 as mnr, 2 as lang, N''Hospitality (Legacy)'' as descr, ''Hospitality'' as val FROM dual
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
