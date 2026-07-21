using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCategoryToDocumentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop existing Category index
            migrationBuilder.Sql("BEGIN EXECUTE IMMEDIATE 'DROP INDEX \"IX_DOC_CATEGORY\"'; EXCEPTION WHEN OTHERS THEN NULL; END;");

            // 2. Add DOCUMENT_TYPE column
            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" ADD \"DOCUMENT_TYPE\" NUMBER(10) DEFAULT 12 NOT NULL");

            // 3. Migrate data from CATEGORY to DOCUMENT_TYPE
            migrationBuilder.Sql(@"
                UPDATE ""SYS_DOCUMENT"" SET ""DOCUMENT_TYPE"" = CASE 
                    WHEN ""CATEGORY"" = 'Contracts' THEN 1
                    WHEN ""CATEGORY"" = 'Reports' THEN 2
                    WHEN ""CATEGORY"" = 'Invoices' THEN 3
                    WHEN ""CATEGORY"" = 'Receipts' THEN 4
                    WHEN ""CATEGORY"" = 'Identification' THEN 5
                    WHEN ""CATEGORY"" = 'Certificates' THEN 6
                    WHEN ""CATEGORY"" = 'Financial' THEN 7
                    WHEN ""CATEGORY"" = 'HR' THEN 8
                    WHEN ""CATEGORY"" = 'Legal' THEN 9
                    WHEN ""CATEGORY"" = 'Technical' THEN 10
                    WHEN ""CATEGORY"" = 'Marketing' THEN 11
                    ELSE 12
                END");

            // 4. Drop CATEGORY column
            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" DROP COLUMN \"CATEGORY\"");

            // 5. Create new Index
            migrationBuilder.Sql("CREATE INDEX \"IX_DOC_TYPE\" ON \"SYS_DOCUMENT\" (\"DOCUMENT_TYPE\")");

            // 6. Seed DocumentCategories (CODE_MGR = 1) in SYS_CODE
            migrationBuilder.Sql(@"
                DECLARE
                    v_mgr NUMBER := 1;
                    v_seed_user NVARCHAR2(100) := 'SystemSeed';
                    v_now TIMESTAMP := CURRENT_TIMESTAMP;
                BEGIN
                    -- 1. Contracts
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 1, 1, N'عقود', 'Contracts', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=1 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 1, 2, 'Contracts', 'Contracts', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=1 AND ""CODE_LANG""=2);

                    -- 2. Reports
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 2, 1, N'تقارير', 'Reports', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=2 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 2, 2, 'Reports', 'Reports', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=2 AND ""CODE_LANG""=2);

                    -- 3. Invoices
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 3, 1, N'فواتير', 'Invoices', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=3 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 3, 2, 'Invoices', 'Invoices', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=3 AND ""CODE_LANG""=2);

                    -- 4. Receipts
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 4, 1, N'إيصالات', 'Receipts', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=4 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 4, 2, 'Receipts', 'Receipts', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=4 AND ""CODE_LANG""=2);

                    -- 5. Identification
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 5, 1, N'بطاقات هوية', 'Identification', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=5 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 5, 2, 'Identification', 'Identification', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=5 AND ""CODE_LANG""=2);

                    -- 6. Certificates
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 6, 1, N'شهادات', 'Certificates', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=6 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 6, 2, 'Certificates', 'Certificates', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=6 AND ""CODE_LANG""=2);

                    -- 7. Financial
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 7, 1, N'مالي', 'Financial', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=7 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 7, 2, 'Financial', 'Financial', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=7 AND ""CODE_LANG""=2);

                    -- 8. HR
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 8, 1, N'الموارد البشرية', 'HR', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=8 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 8, 2, 'HR', 'HR', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=8 AND ""CODE_LANG""=2);

                    -- 9. Legal
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 9, 1, N'قانوني', 'Legal', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=9 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 9, 2, 'Legal', 'Legal', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=9 AND ""CODE_LANG""=2);

                    -- 10. Technical
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 10, 1, N'تقني', 'Technical', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=10 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 10, 2, 'Technical', 'Technical', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=10 AND ""CODE_LANG""=2);

                    -- 11. Marketing
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 11, 1, N'تسويق', 'Marketing', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=11 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 11, 2, 'Marketing', 'Marketing', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=11 AND ""CODE_LANG""=2);

                    -- 12. Other
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 12, 1, N'أخرى', 'Other', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=12 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 12, 2, 'Other', 'Other', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=12 AND ""CODE_LANG""=2);
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("BEGIN EXECUTE IMMEDIATE 'DROP INDEX \"IX_DOC_TYPE\"'; EXCEPTION WHEN OTHERS THEN NULL; END;");
            
            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" ADD \"CATEGORY\" NVARCHAR2(50) NULL");

            migrationBuilder.Sql(@"
                UPDATE ""SYS_DOCUMENT"" SET ""CATEGORY"" = CASE 
                    WHEN ""DOCUMENT_TYPE"" = 1 THEN 'Contracts'
                    WHEN ""DOCUMENT_TYPE"" = 2 THEN 'Reports'
                    WHEN ""DOCUMENT_TYPE"" = 3 THEN 'Invoices'
                    WHEN ""DOCUMENT_TYPE"" = 4 THEN 'Receipts'
                    WHEN ""DOCUMENT_TYPE"" = 5 THEN 'Identification'
                    WHEN ""DOCUMENT_TYPE"" = 6 THEN 'Certificates'
                    WHEN ""DOCUMENT_TYPE"" = 7 THEN 'Financial'
                    WHEN ""DOCUMENT_TYPE"" = 8 THEN 'HR'
                    WHEN ""DOCUMENT_TYPE"" = 9 THEN 'Legal'
                    WHEN ""DOCUMENT_TYPE"" = 10 THEN 'Technical'
                    WHEN ""DOCUMENT_TYPE"" = 11 THEN 'Marketing'
                    ELSE 'Other'
                END");

            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" DROP COLUMN \"DOCUMENT_TYPE\"");
            
            migrationBuilder.Sql("CREATE INDEX \"IX_DOC_CATEGORY\" ON \"SYS_DOCUMENT\" (\"CATEGORY\")");

            migrationBuilder.Sql("DELETE FROM \"SYS_CODE\" WHERE \"CODE_MGR\" = 1;");
        }
    }
}
