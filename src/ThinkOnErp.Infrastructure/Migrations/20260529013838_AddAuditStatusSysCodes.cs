using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditStatusSysCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert AuditStatus SysCode entries (mgr=15)
            // Each code has two rows: CODE_LANG=1 (Arabic), CODE_LANG=2 (English)
            migrationBuilder.Sql(@"
                DECLARE
                    v_seed_user NUMBER := 1;
                    v_now TIMESTAMP := SYSTIMESTAMP;
                    v_mgr CONSTANT NUMBER := 15;
                BEGIN
                    -- Unresolved (mnr=1)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 1, 1, N'غير محلول', 'Unresolved', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=1 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 1, 2, 'Unresolved', 'Unresolved', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=1 AND ""CODE_LANG""=2);

                    -- In Progress (mnr=2)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 2, 1, N'قيد المعالجة', 'In Progress', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=2 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 2, 2, 'In Progress', 'In Progress', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=2 AND ""CODE_LANG""=2);

                    -- Resolved (mnr=3)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 3, 1, N'تم الحل', 'Resolved', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=3 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 3, 2, 'Resolved', 'Resolved', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=3 AND ""CODE_LANG""=2);

                    -- Critical (mnr=4)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 4, 1, N'حرج', 'Critical', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=4 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 4, 2, 'Critical', 'Critical', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=4 AND ""CODE_LANG""=2);

                    COMMIT;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""SYS_CODE"" WHERE ""CODE_MGR"" = 15;
            ");
        }
    }
}
