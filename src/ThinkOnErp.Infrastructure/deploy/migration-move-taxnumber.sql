ALTER TABLE "SYS_BRANCH" ADD "TAX_NUMBER" NVARCHAR2(50)
/

MERGE INTO SYS_BRANCH b
                USING (
                    SELECT c.ID AS COMPANY_ID, c.TAX_NUMBER,
                           COALESCE(c.DEFAULT_BRANCH_ID,
                               (SELECT MIN(b2.ID) FROM SYS_BRANCH b2
                                WHERE b2.COMPANY_ID = c.ID AND b2.IS_HEAD_BRANCH = 'Y')
                           ) AS BRANCH_ID
                    FROM SYS_COMPANY c
                    WHERE c.TAX_NUMBER IS NOT NULL
                ) s
                ON (b.ID = s.BRANCH_ID)
                WHEN MATCHED THEN
                    UPDATE SET b.TAX_NUMBER = s.TAX_NUMBER
/

ALTER TABLE "SYS_COMPANY" DROP COLUMN "TAX_NUMBER"
/

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES (N'20260525131146_MoveTaxNumberToBranch', N'8.0.11')
/

