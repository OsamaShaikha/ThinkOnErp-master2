-- ==============================================================================
-- 106_Seed_Selection_Types_SysCodes.sql
-- Seed POS Selection Types into SYS_CODE for ThinkOn ERP:
--   - CODE_MGR = 33: Selection Types (اختيار فردي / أحادي، اختيار متعدد)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- CODE_MGR = 33 : Selection Types (Header / Group)
-- ------------------------------------------------------------------------------
MERGE INTO "SYS_CODE" t
USING (SELECT 33 AS m, 0 AS n, 1 AS l, N'أنواع الاختيار' AS d, 'SelectionTypes' AS v FROM dual) s
ON (t."CODE_MGR" = s.m AND t."CODE_MNR" = s.n AND t."CODE_LANG" = s.l)
WHEN MATCHED THEN UPDATE SET t."CODE_DESC" = s.d, t."CODE_VALUE" = s.v, t."IS_ACTIVE" = 1, t."UPDATE_DATE" = SYSTIMESTAMP
WHEN NOT MATCHED THEN INSERT ("CODE_MGR", "CODE_MNR", "CODE_LANG", "CODE_DESC", "CODE_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
VALUES (s.m, s.n, s.l, s.d, s.v, 1, 'SEED', SYSTIMESTAMP);

MERGE INTO "SYS_CODE" t
USING (SELECT 33 AS m, 0 AS n, 2 AS l, 'Selection Types' AS d, 'SelectionTypes' AS v FROM dual) s
ON (t."CODE_MGR" = s.m AND t."CODE_MNR" = s.n AND t."CODE_LANG" = s.l)
WHEN MATCHED THEN UPDATE SET t."CODE_DESC" = s.d, t."CODE_VALUE" = s.v, t."IS_ACTIVE" = 1, t."UPDATE_DATE" = SYSTIMESTAMP
WHEN NOT MATCHED THEN INSERT ("CODE_MGR", "CODE_MNR", "CODE_LANG", "CODE_DESC", "CODE_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
VALUES (s.m, s.n, s.l, s.d, s.v, 1, 'SEED', SYSTIMESTAMP);

-- 1: Single
MERGE INTO "SYS_CODE" t
USING (SELECT 33 AS m, 1 AS n, 1 AS l, N'اختيار فردي' AS d, 'SINGLE' AS v FROM dual) s
ON (t."CODE_MGR" = s.m AND t."CODE_MNR" = s.n AND t."CODE_LANG" = s.l)
WHEN MATCHED THEN UPDATE SET t."CODE_DESC" = s.d, t."CODE_VALUE" = s.v, t."IS_ACTIVE" = 1, t."UPDATE_DATE" = SYSTIMESTAMP
WHEN NOT MATCHED THEN INSERT ("CODE_MGR", "CODE_MNR", "CODE_LANG", "CODE_DESC", "CODE_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
VALUES (s.m, s.n, s.l, s.d, s.v, 1, 'SEED', SYSTIMESTAMP);

MERGE INTO "SYS_CODE" t
USING (SELECT 33 AS m, 1 AS n, 2 AS l, 'Single Selection' AS d, 'SINGLE' AS v FROM dual) s
ON (t."CODE_MGR" = s.m AND t."CODE_MNR" = s.n AND t."CODE_LANG" = s.l)
WHEN MATCHED THEN UPDATE SET t."CODE_DESC" = s.d, t."CODE_VALUE" = s.v, t."IS_ACTIVE" = 1, t."UPDATE_DATE" = SYSTIMESTAMP
WHEN NOT MATCHED THEN INSERT ("CODE_MGR", "CODE_MNR", "CODE_LANG", "CODE_DESC", "CODE_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
VALUES (s.m, s.n, s.l, s.d, s.v, 1, 'SEED', SYSTIMESTAMP);

-- 2: Multiple
MERGE INTO "SYS_CODE" t
USING (SELECT 33 AS m, 2 AS n, 1 AS l, N'اختيار متعدد' AS d, 'MULTIPLE' AS v FROM dual) s
ON (t."CODE_MGR" = s.m AND t."CODE_MNR" = s.n AND t."CODE_LANG" = s.l)
WHEN MATCHED THEN UPDATE SET t."CODE_DESC" = s.d, t."CODE_VALUE" = s.v, t."IS_ACTIVE" = 1, t."UPDATE_DATE" = SYSTIMESTAMP
WHEN NOT MATCHED THEN INSERT ("CODE_MGR", "CODE_MNR", "CODE_LANG", "CODE_DESC", "CODE_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
VALUES (s.m, s.n, s.l, s.d, s.v, 1, 'SEED', SYSTIMESTAMP);

MERGE INTO "SYS_CODE" t
USING (SELECT 33 AS m, 2 AS n, 2 AS l, 'Multiple Selection' AS d, 'MULTIPLE' AS v FROM dual) s
ON (t."CODE_MGR" = s.m AND t."CODE_MNR" = s.n AND t."CODE_LANG" = s.l)
WHEN MATCHED THEN UPDATE SET t."CODE_DESC" = s.d, t."CODE_VALUE" = s.v, t."IS_ACTIVE" = 1, t."UPDATE_DATE" = SYSTIMESTAMP
WHEN NOT MATCHED THEN INSERT ("CODE_MGR", "CODE_MNR", "CODE_LANG", "CODE_DESC", "CODE_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
VALUES (s.m, s.n, s.l, s.d, s.v, 1, 'SEED', SYSTIMESTAMP);

COMMIT;
