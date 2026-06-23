using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

/// <summary>
/// Seeds deterministic TECH01 test accounts for SuperAdmin and company-admin permission testing.
/// </summary>
[DbContext(typeof(OracleDbContext))]
[Migration("20260624093000_SeedTech01TestAccounts")]
public sealed class SeedTech01TestAccounts : Migration
{
    private const string SeedUser = "ef-seed-tech01-test-accounts";
    private const string CompanyCode = "TECH01";
    private const string CompanySchema = "THINKONERP_TECH01";

    private const string SuperAdminHash = "U5Co/ZSXIsngbzNKQBq1MICB+yIMoArEGkdo7sIh8K7rlpNjfhKpUb6xt8mUU/JS";
    private const string TechAdminHash = "6pikETVDUSbYwMIYtkQ4+XJJn4WMKiv+yEgtLMxnR6QuJ3Rp0igJ23eThSKSLLQQ";
    private const string AmmanAdminHash = "EQc8j1leeP5nYHMqPNQeTDnty50PVSC+W3N4nCtUKgxfodhrPMN3+n1N/RCtmnrE";
    private const string AqabaUserHash = "vRb2v8AbtB2FlEmz7P+t7pWOjvdZlzWi81MtCYtbveathfb/Ou/cJ6Mlt1GIgFDe";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SeedSuperAdminTestAccount(migrationBuilder);
        SeedCompanyTestAccounts(migrationBuilder);
        SeedCompanyRolePermissions(migrationBuilder);
        SeedAqabaUserPermissionOverrides(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RemoveCompanyTestAccounts(migrationBuilder);

        migrationBuilder.Sql($$"""
            DELETE FROM "SYS_SUPER_ADMIN"
            WHERE "USER_NAME" = 'superadmin_test'
              AND "CREATION_USER" = N'{{SeedUser}}'
            """);
    }

    private static void SeedSuperAdminTestAccount(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            MERGE INTO "SYS_SUPER_ADMIN" target
            USING (
                SELECT
                    NVL((SELECT MAX("Id") FROM "SYS_SUPER_ADMIN"), 0) + 1 AS "Id",
                    'superadmin_test' AS "USER_NAME",
                    '{{SuperAdminHash}}' AS "PASSWORD"
                FROM DUAL
            ) source
            ON (target."USER_NAME" = source."USER_NAME")
            WHEN MATCHED THEN UPDATE SET
                target."IS_ACTIVE" = 1,
                target."UPDATE_USER" = N'{{SeedUser}}',
                target."UPDATE_DATE" = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (
                "Id", "NAME_AR", "NAME_EN", "USER_NAME", "PASSWORD", "EMAIL", "PHONE",
                "TWO_FA_ENABLED", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
            ) VALUES (
                source."Id", N'TECH01 Test SuperAdmin', 'TECH01 Test SuperAdmin',
                source."USER_NAME", source."PASSWORD", 'superadmin.test@thinkonerp.local',
                '+962790000001', 0, 1, N'{{SeedUser}}', SYSTIMESTAMP
            )
            """);
    }

    private static void SeedCompanyTestAccounts(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DECLARE
                v_schema_exists NUMBER := 0;
                v_tables_exist NUMBER := 0;
            BEGIN
                SELECT COUNT(*)
                INTO v_schema_exists
                FROM ALL_USERS
                WHERE USERNAME = '{{CompanySchema}}';

                IF v_schema_exists = 1 THEN
                    SELECT COUNT(*)
                    INTO v_tables_exist
                    FROM ALL_TABLES
                    WHERE OWNER = '{{CompanySchema}}'
                      AND TABLE_NAME IN ('SYS_ROLE', 'SYS_USERS', 'SYS_USERS_ROLES');

                    IF v_tables_exist = 3 THEN
                        EXECUTE IMMEDIATE q'~
                            MERGE INTO "{{CompanySchema}}"."SYS_ROLE" target
                            USING (
                                SELECT
                                    base."MAX_ID" + ROW_NUMBER() OVER (ORDER BY role_rows."NAME_EN") AS "Id",
                                    role_rows."NAME_AR",
                                    role_rows."NAME_EN",
                                    role_rows."NOTE"
                                FROM (
                                    SELECT 'Administrator' AS "NAME_AR", 'Administrator' AS "NAME_EN", 'Full TECH01 company administration test role' AS "NOTE" FROM DUAL
                                    UNION ALL SELECT 'Branch Manager', 'Branch Manager', 'Branch-level administration test role' FROM DUAL
                                    UNION ALL SELECT 'Viewer', 'Viewer', 'Read-only business user test role' FROM DUAL
                                ) role_rows
                                CROSS JOIN (
                                    SELECT NVL(MAX("Id"), 0) AS "MAX_ID"
                                    FROM "{{CompanySchema}}"."SYS_ROLE"
                                ) base
                            ) source
                            ON (target."NAME_EN" = source."NAME_EN")
                            WHEN MATCHED THEN UPDATE SET
                                target."IS_ACTIVE" = 1,
                                target."UPDATE_USER" = N'{{SeedUser}}',
                                target."UPDATE_DATE" = SYSTIMESTAMP
                            WHEN NOT MATCHED THEN INSERT (
                                "Id", "NAME_AR", "NAME_EN", "NOTE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
                            ) VALUES (
                                source."Id", source."NAME_AR", source."NAME_EN", source."NOTE",
                                1, N'{{SeedUser}}', SYSTIMESTAMP
                            )
                        ~';

                        EXECUTE IMMEDIATE q'~
                            MERGE INTO "{{CompanySchema}}"."SYS_USERS" target
                            USING (
                                SELECT
                                    base."MAX_ID" + ROW_NUMBER() OVER (ORDER BY desired."USER_NAME") AS "Id",
                                    desired."NAME_AR",
                                    desired."NAME_EN",
                                    desired."USER_NAME",
                                    desired."PASSWORD",
                                    role_table."Id" AS "ROLE_ID",
                                    branch_table."Id" AS "BRANCH_ID",
                                    company_table."Id" AS "COMPANY_ID",
                                    desired."EMAIL",
                                    desired."IS_ADMIN"
                                FROM (
                                    SELECT 'TECH01 Admin' AS "NAME_AR", 'TECH01 Company Admin' AS "NAME_EN", 'tech_admin' AS "USER_NAME", '{{TechAdminHash}}' AS "PASSWORD", 'Administrator' AS "ROLE_NAME", 'Headquarters' AS "BRANCH_NAME", 'tech.admin@tech01.local' AS "EMAIL", 1 AS "IS_ADMIN" FROM DUAL
                                    UNION ALL SELECT 'Amman Admin', 'Amman Branch Admin', 'amman_admin', '{{AmmanAdminHash}}', 'Branch Manager', 'Amman Branch', 'amman.admin@tech01.local', 1 FROM DUAL
                                    UNION ALL SELECT 'Aqaba User', 'Aqaba Normal User', 'aqaba_user', '{{AqabaUserHash}}', 'Viewer', 'Aqaba Branch', 'aqaba.user@tech01.local', 0 FROM DUAL
                                ) desired
                                JOIN "SYS_COMPANY" company_table
                                  ON company_table."COMPANY_CODE" = N'{{CompanyCode}}'
                                JOIN "SYS_BRANCH" branch_table
                                  ON branch_table."COMPANY_ID" = company_table."Id"
                                 AND branch_table."NAME_EN" = desired."BRANCH_NAME"
                                 AND branch_table."IS_ACTIVE" = 1
                                JOIN "{{CompanySchema}}"."SYS_ROLE" role_table
                                  ON role_table."NAME_EN" = desired."ROLE_NAME"
                                CROSS JOIN (
                                    SELECT NVL(MAX("Id"), 0) AS "MAX_ID"
                                    FROM "{{CompanySchema}}"."SYS_USERS"
                                ) base
                            ) source
                            ON (target."USER_NAME" = source."USER_NAME")
                            WHEN MATCHED THEN UPDATE SET
                                target."PASSWORD" = source."PASSWORD",
                                target."ROLE" = source."ROLE_ID",
                                target."BRANCH_ID" = source."BRANCH_ID",
                                target."COMPANY_ID" = source."COMPANY_ID",
                                target."IS_ACTIVE" = 1,
                                target."IS_ADMIN" = source."IS_ADMIN",
                                target."EMAIL" = source."EMAIL",
                                target."UPDATE_USER" = N'{{SeedUser}}',
                                target."UPDATE_DATE" = SYSTIMESTAMP,
                                target."FORCE_LOGOUT_DATE" = NULL
                            WHEN NOT MATCHED THEN INSERT (
                                "Id", "NAME_AR", "NAME_EN", "USER_NAME", "PASSWORD", "ROLE", "BRANCH_ID",
                                "COMPANY_ID", "EMAIL", "IS_ACTIVE", "IS_ADMIN", "CREATION_USER", "CREATION_DATE"
                            ) VALUES (
                                source."Id", source."NAME_AR", source."NAME_EN", source."USER_NAME",
                                source."PASSWORD", source."ROLE_ID", source."BRANCH_ID", source."COMPANY_ID",
                                source."EMAIL", 1, source."IS_ADMIN", N'{{SeedUser}}', SYSTIMESTAMP
                            )
                        ~';

                        EXECUTE IMMEDIATE q'~
                            MERGE INTO "{{CompanySchema}}"."SYS_USERS_ROLES" target
                            USING (
                                SELECT
                                    base."MAX_ID" + ROW_NUMBER() OVER (ORDER BY user_table."USER_NAME") AS "Id",
                                    user_table."Id" AS "USER_ID",
                                    role_table."Id" AS "ROLE_ID"
                                FROM "{{CompanySchema}}"."SYS_USERS" user_table
                                JOIN "{{CompanySchema}}"."SYS_ROLE" role_table
                                  ON role_table."Id" = user_table."ROLE"
                                CROSS JOIN (
                                    SELECT NVL(MAX("Id"), 0) AS "MAX_ID"
                                    FROM "{{CompanySchema}}"."SYS_USERS_ROLES"
                                ) base
                                WHERE user_table."USER_NAME" IN ('tech_admin', 'amman_admin', 'aqaba_user')
                            ) source
                            ON (
                                target."USER_ID" = source."USER_ID"
                                AND target."ROLE_ID" = source."ROLE_ID"
                            )
                            WHEN NOT MATCHED THEN INSERT (
                                "Id", "USER_ID", "ROLE_ID", "ASSIGNED_DATE", "CREATION_USER", "CREATION_DATE"
                            ) VALUES (
                                source."Id", source."USER_ID", source."ROLE_ID", SYSTIMESTAMP,
                                N'{{SeedUser}}', SYSTIMESTAMP
                            )
                        ~';
                    END IF;
                END IF;
            END;
            """);
    }

    private static void SeedCompanyRolePermissions(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DECLARE
                v_schema_exists NUMBER := 0;
                v_tables_exist NUMBER := 0;
            BEGIN
                SELECT COUNT(*)
                INTO v_schema_exists
                FROM ALL_USERS
                WHERE USERNAME = '{{CompanySchema}}';

                IF v_schema_exists = 1 THEN
                    SELECT COUNT(*)
                    INTO v_tables_exist
                    FROM ALL_TABLES
                    WHERE OWNER = '{{CompanySchema}}'
                      AND TABLE_NAME IN ('SYS_ROLE', 'SYS_ROLE_SCREEN_PERMISSIONS');

                    IF v_tables_exist = 2 THEN
                        EXECUTE IMMEDIATE q'~
                            MERGE INTO "{{CompanySchema}}"."SYS_ROLE_SCREEN_PERMISSIONS" target
                            USING (
                                SELECT
                                    NVL((SELECT MAX("Id") FROM "{{CompanySchema}}"."SYS_ROLE_SCREEN_PERMISSIONS"), 0)
                                        + ROW_NUMBER() OVER (ORDER BY role_table."Id", branch_table."Id", screen_table."Id", sf."FEATURE_ID") AS "Id",
                                    branch_table."Id" AS "BRANCH_ID",
                                    role_table."Id" AS "ROLE_ID",
                                    screen_table."Id" AS "SCREEN_ID",
                                    sf."FEATURE_ID" AS "FEATURE_ID",
                                    CASE
                                        WHEN role_table."NAME_EN" = 'Viewer'
                                             AND feature_table."FEATURE_CODE" <> 'view' THEN 0
                                        ELSE 1
                                    END AS "IS_GRANTED"
                                FROM "SYS_COMPANY" company_table
                                JOIN "SYS_BRANCH" branch_table
                                  ON branch_table."COMPANY_ID" = company_table."Id"
                                 AND branch_table."IS_ACTIVE" = 1
                                JOIN "SYS_BRANCH_SYSTEMS" bs
                                  ON bs."BRANCH_ID" = branch_table."Id"
                                 AND bs."REVOKED_DATE" IS NULL
                                JOIN "SYS_SCREEN" screen_table
                                  ON screen_table."SYSTEM_ID" = bs."SYSTEM_ID"
                                 AND screen_table."IS_ACTIVE" = 1
                                JOIN "SYS_SCREEN_FEATURE" sf
                                  ON sf."SCREEN_ID" = screen_table."Id"
                                JOIN "SYS_FEATURE" feature_table
                                  ON feature_table."Id" = sf."FEATURE_ID"
                                 AND feature_table."IS_ACTIVE" = 1
                                JOIN "{{CompanySchema}}"."SYS_ROLE" role_table
                                  ON role_table."NAME_EN" IN ('Administrator', 'Branch Manager', 'Viewer')
                                LEFT JOIN "SYS_BRANCH_SCREENS" revoked_screen
                                  ON revoked_screen."BRANCH_ID" = branch_table."Id"
                                 AND revoked_screen."SCREEN_ID" = screen_table."Id"
                                LEFT JOIN "SYS_BRANCH_FEATURES" revoked_feature
                                  ON revoked_feature."BRANCH_ID" = branch_table."Id"
                                 AND revoked_feature."SCREEN_ID" = screen_table."Id"
                                 AND revoked_feature."FEATURE_ID" = sf."FEATURE_ID"
                                WHERE company_table."COMPANY_CODE" = N'{{CompanyCode}}'
                                  AND revoked_screen."Id" IS NULL
                                  AND revoked_feature."Id" IS NULL
                            ) source
                            ON (
                                target."BRANCH_ID" = source."BRANCH_ID"
                                AND target."ROLE_ID" = source."ROLE_ID"
                                AND target."SCREEN_ID" = source."SCREEN_ID"
                                AND target."FEATURE_ID" = source."FEATURE_ID"
                            )
                            WHEN MATCHED THEN UPDATE SET
                                target."IS_GRANTED" = source."IS_GRANTED",
                                target."UPDATE_USER" = N'{{SeedUser}}',
                                target."UPDATE_DATE" = SYSTIMESTAMP
                            WHEN NOT MATCHED THEN INSERT (
                                "Id", "BRANCH_ID", "ROLE_ID", "SCREEN_ID", "FEATURE_ID",
                                "IS_GRANTED", "CREATION_USER", "CREATION_DATE"
                            ) VALUES (
                                source."Id", source."BRANCH_ID", source."ROLE_ID", source."SCREEN_ID",
                                source."FEATURE_ID", source."IS_GRANTED", N'{{SeedUser}}', SYSTIMESTAMP
                            )
                        ~';
                    END IF;
                END IF;
            END;
            """);
    }

    private static void SeedAqabaUserPermissionOverrides(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DECLARE
                v_schema_exists NUMBER := 0;
                v_tables_exist NUMBER := 0;
            BEGIN
                SELECT COUNT(*)
                INTO v_schema_exists
                FROM ALL_USERS
                WHERE USERNAME = '{{CompanySchema}}';

                IF v_schema_exists = 1 THEN
                    SELECT COUNT(*)
                    INTO v_tables_exist
                    FROM ALL_TABLES
                    WHERE OWNER = '{{CompanySchema}}'
                      AND TABLE_NAME = 'SYS_USER_SCREEN_PERMISSIONS';

                    IF v_tables_exist = 1 THEN
                        EXECUTE IMMEDIATE q'~
                            MERGE INTO "{{CompanySchema}}"."SYS_USER_SCREEN_PERMISSIONS" target
                            USING (
                                SELECT
                                    NVL((SELECT MAX("Id") FROM "{{CompanySchema}}"."SYS_USER_SCREEN_PERMISSIONS"), 0)
                                        + ROW_NUMBER() OVER (ORDER BY screen_table."Id", feature_table."Id") AS "Id",
                                    branch_table."Id" AS "BRANCH_ID",
                                    user_table."Id" AS "USER_ID",
                                    screen_table."Id" AS "SCREEN_ID",
                                    feature_table."Id" AS "FEATURE_ID"
                                FROM "SYS_COMPANY" company_table
                                JOIN "SYS_BRANCH" branch_table
                                  ON branch_table."COMPANY_ID" = company_table."Id"
                                 AND branch_table."NAME_EN" = 'Aqaba Branch'
                                JOIN "{{CompanySchema}}"."SYS_USERS" user_table
                                  ON user_table."USER_NAME" = 'aqaba_user'
                                JOIN "SYS_BRANCH_SYSTEMS" bs
                                  ON bs."BRANCH_ID" = branch_table."Id"
                                 AND bs."REVOKED_DATE" IS NULL
                                JOIN "SYS_SCREEN" screen_table
                                  ON screen_table."SYSTEM_ID" = bs."SYSTEM_ID"
                                 AND screen_table."IS_ACTIVE" = 1
                                JOIN "SYS_SCREEN_FEATURE" sf
                                  ON sf."SCREEN_ID" = screen_table."Id"
                                JOIN "SYS_FEATURE" feature_table
                                  ON feature_table."Id" = sf."FEATURE_ID"
                                 AND feature_table."FEATURE_CODE" IN ('delete', 'approve', 'reject')
                                WHERE company_table."COMPANY_CODE" = N'{{CompanyCode}}'
                            ) source
                            ON (
                                target."BRANCH_ID" = source."BRANCH_ID"
                                AND target."USER_ID" = source."USER_ID"
                                AND target."SCREEN_ID" = source."SCREEN_ID"
                                AND target."FEATURE_ID" = source."FEATURE_ID"
                            )
                            WHEN MATCHED THEN UPDATE SET
                                target."IS_GRANTED" = 0,
                                target."UPDATE_USER" = N'{{SeedUser}}',
                                target."UPDATE_DATE" = SYSTIMESTAMP
                            WHEN NOT MATCHED THEN INSERT (
                                "Id", "BRANCH_ID", "USER_ID", "SCREEN_ID", "FEATURE_ID",
                                "IS_GRANTED", "CREATION_USER", "CREATION_DATE"
                            ) VALUES (
                                source."Id", source."BRANCH_ID", source."USER_ID", source."SCREEN_ID",
                                source."FEATURE_ID", 0, N'{{SeedUser}}', SYSTIMESTAMP
                            )
                        ~';
                    END IF;
                END IF;
            END;
            """);
    }

    private static void RemoveCompanyTestAccounts(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DECLARE
                v_schema_exists NUMBER := 0;
                v_tables_exist NUMBER := 0;
            BEGIN
                SELECT COUNT(*)
                INTO v_schema_exists
                FROM ALL_USERS
                WHERE USERNAME = '{{CompanySchema}}';

                IF v_schema_exists = 1 THEN
                    SELECT COUNT(*)
                    INTO v_tables_exist
                    FROM ALL_TABLES
                    WHERE OWNER = '{{CompanySchema}}'
                      AND TABLE_NAME IN (
                          'SYS_ROLE', 'SYS_USERS', 'SYS_USERS_ROLES',
                          'SYS_ROLE_SCREEN_PERMISSIONS', 'SYS_USER_SCREEN_PERMISSIONS'
                      );

                    IF v_tables_exist = 5 THEN
                        EXECUTE IMMEDIATE 'DELETE FROM "{{CompanySchema}}"."SYS_USER_SCREEN_PERMISSIONS" WHERE "CREATION_USER" = N''{{SeedUser}}''';
                        EXECUTE IMMEDIATE 'DELETE FROM "{{CompanySchema}}"."SYS_ROLE_SCREEN_PERMISSIONS" WHERE "CREATION_USER" = N''{{SeedUser}}''';
                        EXECUTE IMMEDIATE 'DELETE FROM "{{CompanySchema}}"."SYS_USERS_ROLES" WHERE "CREATION_USER" = N''{{SeedUser}}''';
                        EXECUTE IMMEDIATE 'DELETE FROM "{{CompanySchema}}"."SYS_USERS" WHERE "USER_NAME" IN (''tech_admin'', ''amman_admin'', ''aqaba_user'') AND "CREATION_USER" = N''{{SeedUser}}''';
                        EXECUTE IMMEDIATE 'DELETE FROM "{{CompanySchema}}"."SYS_ROLE" WHERE "NAME_EN" IN (''Branch Manager'', ''Viewer'') AND "CREATION_USER" = N''{{SeedUser}}''';
                    END IF;
                END IF;
            END;
            """);
    }
}
