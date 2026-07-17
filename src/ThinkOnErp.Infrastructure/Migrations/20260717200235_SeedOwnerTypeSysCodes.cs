using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedOwnerTypeSysCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE
                    v_mgr NUMBER := 2;
                    v_seed_user NVARCHAR2(100) := 'SystemSeed';
                    v_now TIMESTAMP := CURRENT_TIMESTAMP;
                BEGIN
                    -- 1. Company (Arabic/English)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 1, 1, N'شركة', 'Company', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=1 AND ""CODE_LANG""=1);
                    
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 1, 2, 'Company', 'Company', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=1 AND ""CODE_LANG""=2);

                    -- 2. Branch (Arabic/English)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 2, 1, N'فرع', 'Branch', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=2 AND ""CODE_LANG""=1);
                    
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 2, 2, 'Branch', 'Branch', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=2 AND ""CODE_LANG""=2);

                    -- 3. SuperAdmin (Arabic/English)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 3, 1, N'المشرف العام', 'SuperAdmin', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=3 AND ""CODE_LANG""=1);
                    
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT v_mgr, 3, 2, 'Super Admin', 'SuperAdmin', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=v_mgr AND ""CODE_MNR""=3 AND ""CODE_LANG""=2);
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"SYS_CODE\" WHERE \"CODE_MGR\" = 2;");
        }
    }
}
