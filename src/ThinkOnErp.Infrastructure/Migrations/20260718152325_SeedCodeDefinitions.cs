using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCodeDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE
                    v_seed_user NVARCHAR2(100) := 'SystemSeed';
                    v_now TIMESTAMP := CURRENT_TIMESTAMP;
                BEGIN
                    -- 1. DocumentCategories
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 1, 0, 1, N'تصنيفات المستندات', 'DocumentCategories', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=1 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 1, 0, 2, 'Document Categories', 'DocumentCategories', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=1 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 2. OwnerTypes
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 2, 0, 1, N'أنواع الملاك', 'OwnerTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=2 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 2, 0, 2, 'Owner Types', 'OwnerTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=2 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 3. ThreatTypes
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 3, 0, 1, N'أنواع التهديدات الأمنية', 'ThreatTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=3 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 3, 0, 2, 'Threat Types', 'ThreatTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=3 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 4. ThreatSeverity
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 4, 0, 1, N'مستويات خطورة التهديد', 'ThreatSeverity', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=4 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 4, 0, 2, 'Threat Severity Levels', 'ThreatSeverity', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=4 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 5. AuditSeverity
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 5, 0, 1, N'مستويات خطورة التدقيق', 'AuditSeverity', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=5 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 5, 0, 2, 'Audit Severity Levels', 'AuditSeverity', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=5 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 6. EventCategories
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 6, 0, 1, N'تصنيفات أحداث التدقيق', 'EventCategories', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=6 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 6, 0, 2, 'Audit Event Categories', 'EventCategories', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=6 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 7. ActorTypes
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 7, 0, 1, N'أنواع الفاعلين', 'ActorTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=7 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 7, 0, 2, 'Actor Types', 'ActorTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=7 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 8. AuditEventTypes
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 8, 0, 1, N'أنواع أحداث السجلات', 'AuditEventTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=8 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 8, 0, 2, 'Audit Event Types', 'AuditEventTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=8 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 9. PayloadLoggingLevels
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 9, 0, 1, N'مستويات تسجيل البيانات', 'PayloadLoggingLevels', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=9 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 9, 0, 2, 'Payload Logging Levels', 'PayloadLoggingLevels', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=9 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 10. HealthStatus
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 10, 0, 1, N'حالة النظام الصحية', 'HealthStatus', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=10 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 10, 0, 2, 'System Health Status', 'HealthStatus', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=10 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 11. MemoryPressure
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 11, 0, 1, N'مستويات ضغط الذاكرة', 'MemoryPressure', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=11 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 11, 0, 2, 'Memory Pressure Status', 'MemoryPressure', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=11 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 12. KeyTypes
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 12, 0, 1, N'أنواع المفاتيح', 'KeyTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=12 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 12, 0, 2, 'Key Types', 'KeyTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=12 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 13. AlertTypes
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 13, 0, 1, N'أنواع التنبيهات', 'AlertTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=13 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 13, 0, 2, 'Alert Types', 'AlertTypes', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=13 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 14. Languages
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 14, 0, 1, N'لغات النظام', 'Languages', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=14 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 14, 0, 2, 'System Languages', 'Languages', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=14 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 15. AuditStatus
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 15, 0, 1, N'حالة التدقيق', 'AuditStatus', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=15 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 15, 0, 2, 'Audit Status', 'AuditStatus', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=15 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"SYS_CODE\" WHERE \"CODE_MNR\" = 0;");
        }
    }
}
