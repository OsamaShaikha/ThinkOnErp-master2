using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

/// <summary>
/// Migration to unify company and branch settings into a single SYS_SETTINGS table.
/// Adds BRANCH_ID (0 = Company default, > 0 = Branch specific override), updates PK,
/// migrates existing branch data, drops redundant tables, and seeds POS defaults.
/// </summary>
public partial class UnifyCompanyAndBranchSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add BRANCH_ID to SYS_SETTINGS if not exists
        migrationBuilder.Sql("""
            DECLARE
                v_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO v_count 
                FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'BRANCH_ID';
                
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("BRANCH_ID" NUMBER(19) DEFAULT 0 NOT NULL)';
                END IF;
            END;
            """);

        // 2. Add extra metadata columns to SYS_SETTINGS if not present
        migrationBuilder.Sql("""
            DECLARE
                v_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'SETTING_KEY';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("SETTING_KEY" VARCHAR2(100) NULL)';
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'MODULE';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("MODULE" VARCHAR2(50) DEFAULT ''GENERAL'')';
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'DATA_TYPE';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("DATA_TYPE" VARCHAR2(20) DEFAULT ''STRING'')';
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'IS_ACTIVE';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("IS_ACTIVE" NUMBER(1) DEFAULT 1)';
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'CREATION_USER';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("CREATION_USER" NVARCHAR2(100) DEFAULT ''SYSTEM'')';
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'CREATION_DATE';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("CREATION_DATE" TIMESTAMP DEFAULT SYSTIMESTAMP)';
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'UPDATE_USER';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("UPDATE_USER" NVARCHAR2(100) NULL)';
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'SYS_SETTINGS' AND COLUMN_NAME = 'UPDATE_DATE';
                IF v_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD ("UPDATE_DATE" TIMESTAMP NULL)';
                END IF;
            END;
            """);

        // 3. Drop old PK constraint on SYS_SETTINGS and create composite PK (BRANCH_ID, SETTING_CODE)
        migrationBuilder.Sql("""
            DECLARE
                v_count NUMBER;
            BEGIN
                FOR c IN (
                    SELECT constraint_name 
                    FROM ALL_CONSTRAINTS 
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'SYS_SETTINGS' AND CONSTRAINT_TYPE = 'P'
                ) LOOP
                    BEGIN
                        EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" DROP CONSTRAINT "' || c.constraint_name || '"';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END LOOP;

                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD CONSTRAINT "PK_SYS_SETTINGS" PRIMARY KEY ("BRANCH_ID", "SETTING_CODE")';
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END;
            """);

        // 4. Migrate any data from POS_BRANCH_SETTING into SYS_SETTINGS
        migrationBuilder.Sql("""
            DECLARE
                v_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO v_count 
                FROM ALL_TABLES 
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'POS_BRANCH_SETTING';

                IF v_count > 0 THEN
                    BEGIN
                        EXECUTE IMMEDIATE '
                            MERGE INTO "SYS_SETTINGS" t
                            USING (
                                SELECT "BRANCH_ID", 53 AS "SETTING_CODE", ''POS_DEFAULT_CASH_ACCOUNT'' AS "SETTING_KEY", N''صندوق الكاشير الافتراضي للفرع'' AS "SETTING_DESC", "DEFAULT_CASH_ACCOUNT" AS "SETTING_VALUE", ''POS'' AS "MODULE"
                                FROM "POS_BRANCH_SETTING" WHERE "DEFAULT_CASH_ACCOUNT" IS NOT NULL
                                UNION ALL
                                SELECT "BRANCH_ID", 54, ''POS_DEFAULT_EXPENSE_ACCOUNT'', N''حساب المصروفات النثرية الافتراضي للفرع'', "DEFAULT_EXPENSE_ACCOUNT", ''POS''
                                FROM "POS_BRANCH_SETTING" WHERE "DEFAULT_EXPENSE_ACCOUNT" IS NOT NULL
                                UNION ALL
                                SELECT "BRANCH_ID", 55, ''POS_DEFAULT_RECEIPT_ACCOUNT'', N''حساب المقبوضات الافتراضي للفرع'', "DEFAULT_RECEIPT_ACCOUNT", ''POS''
                                FROM "POS_BRANCH_SETTING" WHERE "DEFAULT_RECEIPT_ACCOUNT" IS NOT NULL
                                UNION ALL
                                SELECT "BRANCH_ID", 51, ''POS_DEFAULT_WAREHOUSE_ID'', N''المستودع الافتراضي لمبيعات الـ POS للفرع'', TO_CHAR("DEFAULT_WAREHOUSE_ID"), ''POS''
                                FROM "POS_BRANCH_SETTING" WHERE "DEFAULT_WAREHOUSE_ID" IS NOT NULL
                            ) s
                            ON (t."BRANCH_ID" = s."BRANCH_ID" AND t."SETTING_CODE" = s."SETTING_CODE")
                            WHEN MATCHED THEN UPDATE SET t."SETTING_VALUE" = s."SETTING_VALUE"
                            WHEN NOT MATCHED THEN INSERT ("BRANCH_ID", "SETTING_CODE", "SETTING_KEY", "SETTING_DESC", "SETTING_VALUE", "MODULE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                            VALUES (s."BRANCH_ID", s."SETTING_CODE", s."SETTING_KEY", s."SETTING_DESC", s."SETTING_VALUE", s."MODULE", 1, ''SYSTEM'', SYSTIMESTAMP)
                        ';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END IF;
            END;
            """);

        // 5. Seed default company-level POS settings (BRANCH_ID = 0)
        migrationBuilder.Sql("""
            BEGIN
                MERGE INTO "SYS_SETTINGS" t
                USING (
                    SELECT 0 AS "BRANCH_ID", 50 AS "SETTING_CODE", 'POS_STOCK_DEDUCTION_MODE' AS "SETTING_KEY", N'نمط خصم المخزون الافتراضي (1: فوري، 2: تجميعي بتقرير Z، 3: معطل)' AS "SETTING_DESC", '1' AS "SETTING_VALUE", 'POS' AS "MODULE" FROM DUAL UNION ALL
                    SELECT 0, 51, 'POS_DEFAULT_WAREHOUSE_ID', N'المستودع الافتراضي لمبيعات نقطة البيع', '1', 'POS' FROM DUAL UNION ALL
                    SELECT 0, 52, 'POS_SHIFT_CLOSING_MODE', N'نمط إغلاق الوردية الافتراضي (1: إغلاق أعمى، 2: عادي)', '1', 'POS' FROM DUAL UNION ALL
                    SELECT 0, 53, 'POS_DEFAULT_CASH_ACCOUNT', N'صندوق نقطة البيع الافتراضي', '111102', 'POS' FROM DUAL UNION ALL
                    SELECT 0, 54, 'POS_DEFAULT_EXPENSE_ACCOUNT', N'حساب المصروفات النثرية لسندات الصرف', '525101', 'POS' FROM DUAL UNION ALL
                    SELECT 0, 55, 'POS_DEFAULT_RECEIPT_ACCOUNT', N'حساب الإيرادات المتنوعة لسندات القبض', '421101', 'POS' FROM DUAL UNION ALL
                    SELECT 0, 56, 'POS_ALLOW_NEGATIVE_STOCK', N'السماح بالبيع بالسالب في الـ POS', '0', 'POS' FROM DUAL UNION ALL
                    SELECT 0, 58, 'POS_AUTO_PRINT_RECEIPT', N'طباعة الإيصال آلياً عند إتمام الطلب', '1', 'POS' FROM DUAL
                ) s
                ON (t."BRANCH_ID" = s."BRANCH_ID" AND t."SETTING_CODE" = s."SETTING_CODE")
                WHEN NOT MATCHED THEN INSERT ("BRANCH_ID", "SETTING_CODE", "SETTING_KEY", "SETTING_DESC", "SETTING_VALUE", "MODULE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (s."BRANCH_ID", s."SETTING_CODE", s."SETTING_KEY", s."SETTING_DESC", s."SETTING_VALUE", s."MODULE", 1, 'SYSTEM', SYSTIMESTAMP);
            END;
            """);

        // 6. Clean up redundant tables (POS_BRANCH_SETTINGS & POS_BRANCH_SETTING)
        migrationBuilder.Sql("""
            DECLARE
                v_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO v_count FROM ALL_TABLES WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'POS_BRANCH_SETTINGS';
                IF v_count > 0 THEN
                    BEGIN
                        EXECUTE IMMEDIATE 'DROP TABLE "POS_BRANCH_SETTINGS" CASCADE CONSTRAINTS';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END IF;

                SELECT COUNT(*) INTO v_count FROM ALL_TABLES WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'POS_BRANCH_SETTING';
                IF v_count > 0 THEN
                    BEGIN
                        EXECUTE IMMEDIATE 'DROP TABLE "POS_BRANCH_SETTING" CASCADE CONSTRAINTS';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revert PK
        migrationBuilder.Sql("""
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" DROP CONSTRAINT "PK_SYS_SETTINGS"';
                EXECUTE IMMEDIATE 'ALTER TABLE "SYS_SETTINGS" ADD CONSTRAINT "PK_SYS_SETTINGS" PRIMARY KEY ("SETTING_CODE")';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            """);
    }
}
