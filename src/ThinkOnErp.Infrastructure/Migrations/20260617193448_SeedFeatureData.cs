using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedFeatureData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. SysSetting code 7 (ICONS path)
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SETTINGS"" (""SETTING_CODE"", ""SETTING_DESC"", ""SETTING_VALUE"")
                SELECT 7, 'Icons path', '/THINKON_FILES/ICONS/' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SETTINGS"" WHERE ""SETTING_CODE"" = 7);
            ");

            // 2. Generic SysFeature records (reusable permission actions)
            // view, create, edit, delete, approve, reject, export, print
            // ICON is NULL because icons are uploaded via the API (multipart/form-data), not hardcoded
            migrationBuilder.Sql(@"
                BEGIN
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'view', N'عرض', 'View', N'Ability to view records', 'Ability to view records', NULL, 1, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'view');
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'create', N'إنشاء', 'Create', N'Ability to create new records', 'Ability to create new records', NULL, 2, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'create');
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'edit', N'تعديل', 'Edit', N'Ability to edit existing records', 'Ability to edit existing records', NULL, 3, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'edit');
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'delete', N'حذف', 'Delete', N'Ability to delete records', 'Ability to delete records', NULL, 4, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'delete');
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'approve', N'اعتماد', 'Approve', N'Ability to approve records', 'Ability to approve records', NULL, 5, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'approve');
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'reject', N'رفض', 'Reject', N'Ability to reject records', 'Ability to reject records', NULL, 6, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'reject');
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'export', N'تصدير', 'Export', N'Ability to export records', 'Ability to export records', NULL, 7, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'export');
                    INSERT INTO ""SYS_FEATURE"" (""FEATURE_CODE"", ""FEATURE_NAME"", ""FEATURE_NAME_E"", ""DESCRIPTION"", ""DESCRIPTION_E"", ""ICON"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'print', N'طباعة', 'Print', N'Ability to print records', 'Ability to print records', NULL, 8, 1, 'seed', SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" = 'print');
                    COMMIT;
                END;
            ");

            // 3. SysScreenFeature junction (assign generic features to screens)
            // Tickets: view, create, edit, delete
            // Reports: view, export, print
            // Employees: view, create, edit, delete
            // Attendance: view, approve, reject
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'support-tickets' AND f.""FEATURE_CODE"" = 'view'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'support-tickets' AND f.""FEATURE_CODE"" = 'create'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'support-tickets' AND f.""FEATURE_CODE"" = 'edit'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'support-tickets' AND f.""FEATURE_CODE"" = 'delete'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'support-reports' AND f.""FEATURE_CODE"" = 'view'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'support-reports' AND f.""FEATURE_CODE"" = 'export'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'support-reports' AND f.""FEATURE_CODE"" = 'print'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'hr-employees' AND f.""FEATURE_CODE"" = 'view'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'hr-employees' AND f.""FEATURE_CODE"" = 'create'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'hr-employees' AND f.""FEATURE_CODE"" = 'edit'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'hr-employees' AND f.""FEATURE_CODE"" = 'delete'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'hr-attendance' AND f.""FEATURE_CODE"" = 'view'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'hr-attendance' AND f.""FEATURE_CODE"" = 'approve'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""SYS_SCREEN_FEATURE"" (""SCREEN_ID"", ""FEATURE_ID"")
                SELECT s.""Id"", f.""Id""
                FROM ""SYS_SCREEN"" s, ""SYS_FEATURE"" f
                WHERE s.""SCREEN_CODE"" = 'hr-attendance' AND f.""FEATURE_CODE"" = 'reject'
                AND NOT EXISTS (SELECT 1 FROM ""SYS_SCREEN_FEATURE"" sf WHERE sf.""SCREEN_ID"" = s.""Id"" AND sf.""FEATURE_ID"" = f.""Id"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                BEGIN
                    DELETE FROM ""SYS_SCREEN_FEATURE"" WHERE ""SCREEN_ID"" IN (
                        SELECT ""Id"" FROM ""SYS_SCREEN"" WHERE ""SCREEN_CODE"" IN ('support-tickets', 'support-reports', 'hr-employees', 'hr-attendance')
                    ) AND ""FEATURE_ID"" IN (
                        SELECT ""Id"" FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" IN ('view', 'create', 'edit', 'delete', 'approve', 'reject', 'export', 'print')
                    );
                    DELETE FROM ""SYS_FEATURE"" WHERE ""FEATURE_CODE"" IN ('view', 'create', 'edit', 'delete', 'approve', 'reject', 'export', 'print');
                    DELETE FROM ""SYS_SETTINGS"" WHERE ""SETTING_CODE"" = 7;
                    COMMIT;
                END;
            ");
        }
    }
}
