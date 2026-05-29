using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminActorTypeSysCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert ADMIN ActorType SysCode entry (mgr=7, mnr=6)
            // Each code has two rows: CODE_LANG=1 (Arabic), CODE_LANG=2 (English)
            migrationBuilder.Sql(@"
                DECLARE
                    v_seed_user NUMBER := 1;
                    v_now TIMESTAMP := SYSTIMESTAMP;
                    v_mgr CONSTANT NUMBER := 7;
                BEGIN
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 6, 1, N'مسؤول', 'ADMIN', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=6 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 6, 2, 'Admin', 'ADMIN', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=6 AND ""CODE_LANG""=2);

                    COMMIT;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""SYS_CODE"" WHERE ""CODE_MGR"" = 7 AND ""CODE_MNR"" = 6;
            ");
        }
    }
}
