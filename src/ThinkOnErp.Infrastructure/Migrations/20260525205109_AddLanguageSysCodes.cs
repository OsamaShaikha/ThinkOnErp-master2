using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageSysCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                MERGE INTO ""SYS_CODE"" t
                USING (
                    SELECT 14 AS CODE_MGR, 1 AS CODE_MNR, 1 AS CODE_LANG, CAST('العربية' AS NVARCHAR2(1000)) AS CODE_DESC, 1 AS IS_ACTIVE, CAST('seed' AS NVARCHAR2(100)) AS CREATION_USER, SYSTIMESTAMP AS CREATION_DATE FROM DUAL UNION ALL
                    SELECT 14, 1, 2, CAST('Arabic' AS NVARCHAR2(1000)), 1, CAST('seed' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL UNION ALL
                    SELECT 14, 2, 1, CAST('الإنجليزية' AS NVARCHAR2(1000)), 1, CAST('seed' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL UNION ALL
                    SELECT 14, 2, 2, CAST('English' AS NVARCHAR2(1000)), 1, CAST('seed' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                ) s
                ON (t.""CODE_MGR"" = s.CODE_MGR AND t.""CODE_MNR"" = s.CODE_MNR AND t.""CODE_LANG"" = s.CODE_LANG)
                WHEN NOT MATCHED THEN
                    INSERT (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    VALUES (s.CODE_MGR, s.CODE_MNR, s.CODE_LANG, s.CODE_DESC, s.IS_ACTIVE, s.CREATION_USER, s.CREATION_DATE)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""SYS_CODE"" WHERE ""CODE_MGR"" = 14
            ");
        }
    }
}
