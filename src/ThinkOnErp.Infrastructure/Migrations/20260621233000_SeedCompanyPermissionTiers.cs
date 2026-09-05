using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

/// <summary>
/// Seeds a deterministic company hierarchy and its two permission tiers:
/// SuperAdmin branch provisioning in the master schema, followed by
/// Administrator role grants in the existing company tenant schema.
/// </summary>
[DbContext(typeof(OracleDbContext))]
[Migration("20260621233000_SeedCompanyPermissionTiers")]
public sealed class SeedCompanyPermissionTiers : Migration
{
    private const string SeedUser = "ef-seed-permission-tiers";
    private const string CompanyCode = "TECH01";
    private const string CompanySchema = "THINKONERP_TECH01";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SeedCompanyAndBranches(migrationBuilder);
        SeedSuperAdminTier(migrationBuilder);
        SeedCompanyAdminTier(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RemoveCompanyAdminTier(migrationBuilder);

        migrationBuilder.Sql($$"""
            DELETE FROM "SYS_BRANCH_SYSTEMS"
            WHERE "CREATION_USER" = N'{{SeedUser}}'
              AND "BRANCH_ID" IN (
                  SELECT b."Id"
                  FROM "SYS_BRANCH" b
                  JOIN "SYS_COMPANY" c ON c."Id" = b."COMPANY_ID"
                  WHERE c."COMPANY_CODE" = N'{{CompanyCode}}'
              )
            """);

        migrationBuilder.Sql($$"""
            UPDATE "SYS_COMPANY"
            SET "DEFAULT_BRANCH_ID" = NULL,
                "UPDATE_USER" = N'{{SeedUser}}',
                "UPDATE_DATE" = SYSTIMESTAMP
            WHERE "COMPANY_CODE" = N'{{CompanyCode}}'
              AND "CREATION_USER" = N'{{SeedUser}}'
            """);

        migrationBuilder.Sql($$"""
            DELETE FROM "SYS_BRANCH"
            WHERE "COMPANY_ID" = (
                SELECT "Id" FROM "SYS_COMPANY" WHERE "COMPANY_CODE" = N'{{CompanyCode}}'
            )
              AND "CREATION_USER" = N'{{SeedUser}}'
            """);

        migrationBuilder.Sql($$"""
            DELETE FROM "SYS_COMPANY"
            WHERE "COMPANY_CODE" = N'{{CompanyCode}}'
              AND "CREATION_USER" = N'{{SeedUser}}'
            """);
    }

    private static void SeedCompanyAndBranches(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            MERGE INTO "SYS_COMPANY" target
            USING (
                SELECT
                    N'شركة التقنية' AS "NAME_LOCAL",
                    N'ThinkOnERP Technology Company' AS "NAME_EN",
                    N'شركة التقنية لأنظمة تخطيط الموارد' AS "LEGAL_NAME",
                    N'ThinkOnERP Technology Company LLC' AS "LEGAL_NAME_E",
                    N'{{CompanyCode}}' AS "COMPANY_CODE",
                    N'{{CompanySchema}}' AS "COMPANY_SCHEMA",
                    (SELECT MIN("Id") FROM "SYS_CURRENCY" WHERE "SHORT_DESC_E" = N'SAR') AS "CURR_ID",
                    (SELECT MIN("Id") FROM "SYS_SUPER_ADMIN" WHERE "IS_ACTIVE" = 1) AS "SUPER_ADMIN_ID"
                FROM DUAL
            ) source
            ON (target."COMPANY_CODE" = source."COMPANY_CODE")
            WHEN MATCHED THEN UPDATE SET
                target."COMPANY_SCHEMA" = COALESCE(target."COMPANY_SCHEMA", source."COMPANY_SCHEMA"),
                target."UPDATE_USER" = N'{{SeedUser}}',
                target."UPDATE_DATE" = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (
                "NAME_LOCAL", "NAME_EN", "LEGAL_NAME", "LEGAL_NAME_E",
                "COMPANY_CODE", "COMPANY_SCHEMA", "CURR_ID",
                "CREATED_BY_SUPER_ADMIN_ID", "IS_ACTIVE",
                "CREATION_USER", "CREATION_DATE"
            ) VALUES (
                source."NAME_LOCAL", source."NAME_EN", source."LEGAL_NAME", source."LEGAL_NAME_E",
                source."COMPANY_CODE", source."COMPANY_SCHEMA", source."CURR_ID",
                source."SUPER_ADMIN_ID", 1,
                N'{{SeedUser}}', SYSTIMESTAMP
            )
            """);

        SeedBranch(
            migrationBuilder,
            "Headquarters",
            "المركز الرئيسي",
            "hq@thinkonerp.example",
            "+96265000001",
            "+962790000001",
            "TECH01-HQ-TAX",
            true);

        SeedBranch(
            migrationBuilder,
            "Amman Branch",
            "فرع عمّان",
            "amman@thinkonerp.example",
            "+96265000002",
            "+962790000002",
            "TECH01-AMMAN-TAX",
            false);

        SeedBranch(
            migrationBuilder,
            "Aqaba Branch",
            "فرع العقبة",
            "aqaba@thinkonerp.example",
            "+96232000003",
            "+962790000003",
            "TECH01-AQABA-TAX",
            false);

        migrationBuilder.Sql($$"""
            UPDATE "SYS_COMPANY" c
            SET c."DEFAULT_BRANCH_ID" = (
                    SELECT MIN(b."Id")
                    FROM "SYS_BRANCH" b
                    WHERE b."COMPANY_ID" = c."Id"
                      AND b."IS_HEAD_BRANCH" = 1
                ),
                c."UPDATE_USER" = N'{{SeedUser}}',
                c."UPDATE_DATE" = SYSTIMESTAMP
            WHERE c."COMPANY_CODE" = N'{{CompanyCode}}'
              AND c."DEFAULT_BRANCH_ID" IS NULL
            """);
    }

    private static void SeedBranch(
        MigrationBuilder migrationBuilder,
        string nameEn,
        string nameAr,
        string email,
        string phone,
        string mobile,
        string taxNumber,
        bool isHeadBranch)
    {
        var headBranchValue = isHeadBranch ? 1 : 0;

        migrationBuilder.Sql($$"""
            MERGE INTO "SYS_BRANCH" target
            USING (
                SELECT
                    c."Id" AS "COMPANY_ID",
                    N'{{nameAr}}' AS "NAME_LOCAL",
                    N'{{nameEn}}' AS "NAME_EN",
                    N'{{email}}' AS "EMAIL",
                    N'{{phone}}' AS "PHONE",
                    N'{{mobile}}' AS "MOBILE",
                    N'{{taxNumber}}' AS "TAX_NUMBER",
                    {{headBranchValue}} AS "IS_HEAD_BRANCH",
                    c."CURR_ID" AS "BASE_CURRENCY_ID"
                FROM "SYS_COMPANY" c
                WHERE c."COMPANY_CODE" = N'{{CompanyCode}}'
            ) source
            ON (
                target."COMPANY_ID" = source."COMPANY_ID"
                AND target."NAME_EN" = source."NAME_EN"
            )
            WHEN MATCHED THEN UPDATE SET
                target."IS_ACTIVE" = 1,
                target."UPDATE_USER" = N'{{SeedUser}}',
                target."UPDATE_DATE" = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (
                "COMPANY_ID", "NAME_LOCAL", "NAME_EN", "EMAIL", "PHONE", "MOBILE",
                "TAX_NUMBER", "IS_HEAD_BRANCH", "BASE_CURRENCY_ID",
                "DEFAULT_LANG", "ROUNDING_RULES", "IS_ACTIVE",
                "CREATION_USER", "CREATION_DATE"
            ) VALUES (
                source."COMPANY_ID", source."NAME_LOCAL", source."NAME_EN",
                source."EMAIL", source."PHONE", source."MOBILE",
                source."TAX_NUMBER", source."IS_HEAD_BRANCH", source."BASE_CURRENCY_ID",
                2, 1, 1,
                N'{{SeedUser}}', SYSTIMESTAMP
            )
            """);
    }

    private static void SeedSuperAdminTier(MigrationBuilder migrationBuilder)
    {
        SeedBranchSystems(
            migrationBuilder,
            "Headquarters",
            "support", "hr", "administration", "security", "accounting", "inventory");

        SeedBranchSystems(
            migrationBuilder,
            "Amman Branch",
            "support", "hr", "accounting", "inventory");

        SeedBranchSystems(
            migrationBuilder,
            "Aqaba Branch",
            "support", "inventory", "pos");
    }

    private static void SeedBranchSystems(
        MigrationBuilder migrationBuilder,
        string branchName,
        params string[] systemCodes)
    {
        var quotedCodes = string.Join(", ", systemCodes.Select(code => $"N'{code.Replace("'", "''")}'"));

        migrationBuilder.Sql($$"""
            MERGE INTO "SYS_BRANCH_SYSTEMS" target
            USING (
                SELECT
                    b."Id" AS "BRANCH_ID",
                    s."Id" AS "SYSTEM_ID",
                    (SELECT MIN(sa."Id") FROM "SYS_SUPER_ADMIN" sa WHERE sa."IS_ACTIVE" = 1) AS "GRANTED_BY"
                FROM "SYS_BRANCH" b
                JOIN "SYS_COMPANY" c ON c."Id" = b."COMPANY_ID"
                CROSS JOIN "SYS_SYSTEM" s
                WHERE c."COMPANY_CODE" = N'{{CompanyCode}}'
                  AND b."NAME_EN" = N'{{branchName}}'
                  AND s."SYSTEM_CODE" IN ({{quotedCodes}})
                  AND s."IS_ACTIVE" = 1
            ) source
            ON (
                target."BRANCH_ID" = source."BRANCH_ID"
                AND target."SYSTEM_ID" = source."SYSTEM_ID"
            )
            WHEN MATCHED THEN UPDATE SET
                target."REVOKED_DATE" = NULL,
                target."GRANTED_BY" = source."GRANTED_BY",
                target."GRANTED_DATE" = SYSTIMESTAMP,
                target."UPDATE_USER" = N'{{SeedUser}}',
                target."UPDATE_DATE" = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (
                "BRANCH_ID", "SYSTEM_ID", "GRANTED_BY", "GRANTED_DATE",
                "NOTES", "CREATION_USER", "CREATION_DATE"
            ) VALUES (
                source."BRANCH_ID", source."SYSTEM_ID", source."GRANTED_BY", SYSTIMESTAMP,
                N'Seeded by EF migration: SuperAdmin permission tier',
                N'{{SeedUser}}', SYSTIMESTAMP
            )
            """);
    }

    private static void SeedCompanyAdminTier(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DECLARE
                v_schema_exists NUMBER := 0;
                v_tables_exist NUMBER := 0;
                v_role_id NUMBER;
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
                        BEGIN
                            EXECUTE IMMEDIATE
                                'SELECT MIN("Id") FROM "{{CompanySchema}}"."SYS_ROLE" ' ||
                                'WHERE "NAME_EN" = N''Administrator'''
                            INTO v_role_id;
                        EXCEPTION
                            WHEN NO_DATA_FOUND THEN
                                v_role_id := NULL;
                        END;

                        IF v_role_id IS NULL THEN
                            EXECUTE IMMEDIATE
                                'SELECT NVL(MAX("Id"), 0) + 1 FROM "{{CompanySchema}}"."SYS_ROLE"'
                            INTO v_role_id;

                            EXECUTE IMMEDIATE
                                'INSERT INTO "{{CompanySchema}}"."SYS_ROLE" ' ||
                                '("Id", "NAME_LOCAL", "NAME_EN", "NOTE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE") ' ||
                                'VALUES (:1, N''مدير النظام'', ''Administrator'', ' ||
                                'N''Company administrator permission tier'', 1, N''{{SeedUser}}'', SYSTIMESTAMP)'
                            USING v_role_id;
                        END IF;

                        EXECUTE IMMEDIATE '
                            MERGE INTO "{{CompanySchema}}"."SYS_ROLE_SCREEN_PERMISSIONS" target
                            USING (
                                SELECT
                                    NVL((
                                        SELECT MAX(p."Id")
                                        FROM "{{CompanySchema}}"."SYS_ROLE_SCREEN_PERMISSIONS" p
                                    ), 0) + ROW_NUMBER() OVER (
                                        ORDER BY b."Id", screen."Id", sf."FEATURE_ID"
                                    ) AS "PERMISSION_ID",
                                    b."Id" AS "BRANCH_ID",
                                    :role_id AS "ROLE_ID",
                                    screen."Id" AS "SCREEN_ID",
                                    sf."FEATURE_ID" AS "FEATURE_ID"
                                FROM "SYS_COMPANY" c
                                JOIN "SYS_BRANCH" b
                                  ON b."COMPANY_ID" = c."Id"
                                 AND b."IS_ACTIVE" = 1
                                JOIN "SYS_BRANCH_SYSTEMS" bs
                                  ON bs."BRANCH_ID" = b."Id"
                                 AND bs."REVOKED_DATE" IS NULL
                                JOIN "SYS_SCREEN" screen
                                  ON screen."SYSTEM_ID" = bs."SYSTEM_ID"
                                 AND screen."IS_ACTIVE" = 1
                                JOIN "SYS_SCREEN_FEATURE" sf
                                  ON sf."SCREEN_ID" = screen."Id"
                                JOIN "SYS_FEATURE" feature
                                  ON feature."Id" = sf."FEATURE_ID"
                                 AND feature."IS_ACTIVE" = 1
                                LEFT JOIN "SYS_BRANCH_SCREENS" revoked_screen
                                  ON revoked_screen."BRANCH_ID" = b."Id"
                                 AND revoked_screen."SCREEN_ID" = screen."Id"
                                LEFT JOIN "SYS_BRANCH_FEATURES" revoked_feature
                                  ON revoked_feature."BRANCH_ID" = b."Id"
                                 AND revoked_feature."SCREEN_ID" = screen."Id"
                                 AND revoked_feature."FEATURE_ID" = sf."FEATURE_ID"
                                WHERE c."COMPANY_CODE" = N''{{CompanyCode}}''
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
                                target."IS_GRANTED" = 1,
                                target."UPDATE_USER" = N''{{SeedUser}}'',
                                target."UPDATE_DATE" = SYSTIMESTAMP
                            WHEN NOT MATCHED THEN INSERT (
                                "Id", "BRANCH_ID", "ROLE_ID", "SCREEN_ID", "FEATURE_ID",
                                "IS_GRANTED", "CREATION_USER", "CREATION_DATE"
                            ) VALUES (
                                source."PERMISSION_ID", source."BRANCH_ID", source."ROLE_ID",
                                source."SCREEN_ID", source."FEATURE_ID",
                                1, N''{{SeedUser}}'', SYSTIMESTAMP
                            )'
                        USING v_role_id;
                    END IF;
                END IF;
            END;
            """);
    }

    private static void RemoveCompanyAdminTier(MigrationBuilder migrationBuilder)
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
                      AND TABLE_NAME = 'SYS_ROLE_SCREEN_PERMISSIONS';

                    IF v_tables_exist = 1 THEN
                        EXECUTE IMMEDIATE
                            'DELETE FROM "{{CompanySchema}}"."SYS_ROLE_SCREEN_PERMISSIONS" ' ||
                            'WHERE "CREATION_USER" = N''{{SeedUser}}''';
                    END IF;
                END IF;
            END;
            """);
    }
}
