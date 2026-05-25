using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSysSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                MERGE INTO ""SYS_SETTINGS"" t
                USING (
                    SELECT 1 AS SETTING_CODE, CAST('Upload files path' AS NVARCHAR2(500)) AS SETTING_DESC, CAST('/THINKON_FILES/UPLOADS/' AS NVARCHAR2(2000)) AS SETTING_VALUE FROM DUAL UNION ALL
                    SELECT 2, CAST('Maximum file size (bytes)' AS NVARCHAR2(500)), CAST('52428800' AS NVARCHAR2(2000)) FROM DUAL UNION ALL
                    SELECT 3, CAST('Allowed file extensions' AS NVARCHAR2(500)), CAST('.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.jpg,.jpeg,.png,.gif,.zip,.rar,.7z' AS NVARCHAR2(2000)) FROM DUAL UNION ALL
                    SELECT 4, CAST('Logs path' AS NVARCHAR2(500)), CAST('/THINKON_FILES/LOGS/' AS NVARCHAR2(2000)) FROM DUAL UNION ALL
                    SELECT 5, CAST('Audit fallback path' AS NVARCHAR2(500)), CAST('/THINKON_FILES/LOGS/audit-fallback/' AS NVARCHAR2(2000)) FROM DUAL
                ) s
                ON (t.""SETTING_CODE"" = s.SETTING_CODE)
                WHEN NOT MATCHED THEN
                    INSERT (""SETTING_CODE"", ""SETTING_DESC"", ""SETTING_VALUE"")
                    VALUES (s.SETTING_CODE, s.SETTING_DESC, s.SETTING_VALUE)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""SYS_SETTINGS"" WHERE ""SETTING_CODE"" IN (1,2,3,4,5)");
        }
    }
}
