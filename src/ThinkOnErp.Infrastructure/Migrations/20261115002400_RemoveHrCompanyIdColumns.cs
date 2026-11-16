using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115002400_RemoveHrCompanyIdColumns")]
public sealed class RemoveHrCompanyIdColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                object_count NUMBER;
                duplicate_count NUMBER;
                column_count NUMBER;
                constraint_count NUMBER;
                index_count NUMBER;
                index_columns VARCHAR2(4000);

                PROCEDURE ensure_index(p_index_name VARCHAR2, p_table_name VARCHAR2, p_columns_expected VARCHAR2, p_unique_expected VARCHAR2) IS
                    existing_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO existing_count
                    FROM ALL_INDEXES
                    WHERE OWNER = owner_name AND INDEX_NAME = p_index_name;
                    IF existing_count = 0 THEN
                        EXECUTE IMMEDIATE 'CREATE ' || CASE p_unique_expected WHEN 'UNIQUE' THEN 'UNIQUE ' ELSE '' END ||
                            'INDEX ' || p_index_name || ' ON ' || p_table_name || ' (' || p_columns_expected || ')';
                    ELSE
                        SELECT COUNT(*) INTO existing_count
                        FROM ALL_INDEXES i
                        WHERE i.OWNER = owner_name AND i.INDEX_NAME = p_index_name AND i.TABLE_NAME = p_table_name
                          AND i.UNIQUENESS = p_unique_expected AND i.STATUS = 'VALID';
                        SELECT LISTAGG(COLUMN_NAME, ',') WITHIN GROUP (ORDER BY COLUMN_POSITION)
                        INTO index_columns FROM ALL_IND_COLUMNS
                        WHERE INDEX_OWNER = owner_name AND INDEX_NAME = p_index_name;
                        IF existing_count <> 1 OR index_columns <> p_columns_expected THEN
                            RAISE_APPLICATION_ERROR(-20360, p_index_name || ' exists with an incompatible definition; no safe continuation.');
                        END IF;
                    END IF;
                END;
            BEGIN
                -- All expected tenant tables are checked before the first DDL statement.
                FOR t IN (
                    SELECT COLUMN_VALUE AS table_name
                    FROM TABLE(SYS.ODCIVARCHAR2LIST(
                        'HR_ATTENDANCE_POLICY', 'HR_ATTENDANCE_TIME_SETTINGS', 'HR_BRANCH_ATTENDANCE_LOCATION',
                        'HR_DEDUCTION_POLICY', 'HR_END_OF_SERVICE_POLICY', 'HR_END_OF_SERVICE_SETTLEMENT',
                        'HR_ACCOUNTING_HANDOFF', 'HR_OVERTIME_RULE', 'HR_PAYROLL_ADJUSTMENT',
                        'HR_PAYROLL_CURRENCY_PRECISION', 'HR_PAYROLL_PERIOD', 'HR_PRORATION_POLICY',
                        'HR_PUBLIC_HOLIDAY', 'HR_SSC_POLICY', 'HR_SHORT_LEAVE_POLICY',
                        'HR_SHORT_LEAVE_REQUEST', 'HR_TAX_POLICY', 'HR_WORK_CALENDAR'
                    ))
                ) LOOP
                    SELECT COUNT(*) INTO object_count FROM ALL_TABLES
                    WHERE OWNER = owner_name AND TABLE_NAME = t.table_name;
                    IF object_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20361, t.table_name || ' is missing in the current schema; no DDL was applied.');
                    END IF;
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLUMNS
                    WHERE OWNER = owner_name AND TABLE_NAME = t.table_name AND COLUMN_NAME = 'COMPANY_ID';
                    IF column_count = 1 THEN
                        EXECUTE IMMEDIATE 'SELECT COUNT(DISTINCT COMPANY_ID) FROM ' || t.table_name INTO duplicate_count;
                        IF duplicate_count > 1 THEN
                            RAISE_APPLICATION_ERROR(-20368, t.table_name || ' contains multiple COMPANY_ID values in one tenant schema; no DDL was applied.');
                        END IF;
                    END IF;
                END LOOP;

                -- A tenant schema must contain one logical company. If old data has
                -- company-specific duplicates, stop before changing any table.
                SELECT COUNT(*) INTO duplicate_count FROM (
                    SELECT CURRENCY_CODE FROM HR_PAYROLL_CURRENCY_PRECISION
                    GROUP BY CURRENCY_CODE HAVING COUNT(*) > 1
                );
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20362, 'Duplicate payroll currency precision rows exist in this schema; review them before removing COMPANY_ID.');
                END IF;
                SELECT COUNT(*) INTO duplicate_count FROM HR_PAYROLL_CURRENCY_PRECISION WHERE CURRENCY_CODE IS NULL;
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20369, 'Null payroll currency codes prevent the schema-scoped primary key; no DDL was applied.');
                END IF;

                SELECT COUNT(*) INTO duplicate_count FROM (
                    SELECT SOURCE_TYPE, SOURCE_ID FROM HR_PAYROLL_ADJUSTMENT
                    WHERE SOURCE_TYPE IS NOT NULL OR SOURCE_ID IS NOT NULL
                    GROUP BY SOURCE_TYPE, SOURCE_ID HAVING COUNT(*) > 1
                );
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20363, 'Duplicate payroll adjustment source keys exist in this schema; review them before removing COMPANY_ID.');
                END IF;

                SELECT COUNT(*) INTO duplicate_count FROM (
                    SELECT SOURCE_TYPE, SOURCE_ID, SOURCE_VERSION, OPERATION FROM HR_ACCOUNTING_HANDOFF
                    GROUP BY SOURCE_TYPE, SOURCE_ID, SOURCE_VERSION, OPERATION HAVING COUNT(*) > 1
                );
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20364, 'Duplicate accounting handoff source keys exist in this schema; review them before removing COMPANY_ID.');
                END IF;

                SELECT COUNT(*) INTO duplicate_count FROM (
                    SELECT HANDOFF_REFERENCE FROM HR_ACCOUNTING_HANDOFF
                    GROUP BY HANDOFF_REFERENCE HAVING COUNT(*) > 1
                );
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20365, 'Duplicate accounting handoff references exist in this schema; review them before removing COMPANY_ID.');
                END IF;
                SELECT COUNT(*) INTO duplicate_count FROM HR_ACCOUNTING_HANDOFF WHERE HANDOFF_REFERENCE IS NULL;
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20370, 'Null accounting handoff references prevent the schema-scoped unique key; no DDL was applied.');
                END IF;

                SELECT COUNT(*) INTO object_count FROM HR_ATTENDANCE_TIME_SETTINGS;
                IF object_count > 1 THEN
                    RAISE_APPLICATION_ERROR(-20366, 'More than one attendance time settings row exists in this tenant schema; merge/review it before migration.');
                END IF;

                -- Replace company-scoped singleton/unique keys with schema-scoped keys.
                SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLUMNS
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_ATTENDANCE_TIME_SETTINGS' AND COLUMN_NAME = 'ID';
                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_ATTENDANCE_TIME_SETTINGS ADD (ID NUMBER(1) DEFAULT 1 NOT NULL)';
                ELSE
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLUMNS
                    WHERE OWNER = owner_name AND TABLE_NAME = 'HR_ATTENDANCE_TIME_SETTINGS'
                      AND COLUMN_NAME = 'ID' AND DATA_TYPE = 'NUMBER' AND DATA_PRECISION = 1 AND NULLABLE = 'N';
                    IF column_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20367, 'HR_ATTENDANCE_TIME_SETTINGS.ID exists with an incompatible definition.');
                    END IF;
                END IF;

                FOR t IN (
                    SELECT COLUMN_VALUE AS table_name
                    FROM TABLE(SYS.ODCIVARCHAR2LIST(
                        'HR_ATTENDANCE_POLICY', 'HR_ATTENDANCE_TIME_SETTINGS', 'HR_BRANCH_ATTENDANCE_LOCATION',
                        'HR_DEDUCTION_POLICY', 'HR_END_OF_SERVICE_POLICY', 'HR_END_OF_SERVICE_SETTLEMENT',
                        'HR_ACCOUNTING_HANDOFF', 'HR_OVERTIME_RULE', 'HR_PAYROLL_ADJUSTMENT',
                        'HR_PAYROLL_CURRENCY_PRECISION', 'HR_PAYROLL_PERIOD', 'HR_PRORATION_POLICY',
                        'HR_PUBLIC_HOLIDAY', 'HR_SSC_POLICY', 'HR_SHORT_LEAVE_POLICY',
                        'HR_SHORT_LEAVE_REQUEST', 'HR_TAX_POLICY', 'HR_WORK_CALENDAR'
                    ))
                ) LOOP
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLUMNS
                    WHERE OWNER = owner_name AND TABLE_NAME = t.table_name AND COLUMN_NAME = 'COMPANY_ID';
                    IF column_count = 1 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE ' || t.table_name || ' DROP COLUMN COMPANY_ID CASCADE CONSTRAINTS';
                    END IF;
                END LOOP;

                SELECT COUNT(*) INTO constraint_count FROM ALL_CONSTRAINTS
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_ATTENDANCE_TIME_SETTINGS'
                  AND CONSTRAINT_NAME = 'PK_HR_ATT_TIME_SETTINGS' AND CONSTRAINT_TYPE = 'P';
                IF constraint_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_ATTENDANCE_TIME_SETTINGS ADD CONSTRAINT PK_HR_ATT_TIME_SETTINGS PRIMARY KEY (ID)';
                END IF;

                SELECT COUNT(*) INTO constraint_count FROM ALL_CONSTRAINTS
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_PAYROLL_CURRENCY_PRECISION'
                  AND CONSTRAINT_TYPE = 'P';
                IF constraint_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_PAYROLL_CURRENCY_PRECISION ADD CONSTRAINT PK_HR_PAYROLL_CURRENCY_PRECISION PRIMARY KEY (CURRENCY_CODE)';
                END IF;

                ensure_index('IX_HR_EOS_EMP_STATUS', 'HR_END_OF_SERVICE_SETTLEMENT', 'EMPLOYEE_CODE,STATUS', 'NONUNIQUE');
                ensure_index('IX_HR_EOS_POL_ACTIVE', 'HR_END_OF_SERVICE_POLICY', 'IS_ACTIVE', 'NONUNIQUE');
                ensure_index('UX_HR_HANDOFF_SOURCE', 'HR_ACCOUNTING_HANDOFF', 'SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION,OPERATION', 'UNIQUE');
                ensure_index('UX_HR_HANDOFF_REF', 'HR_ACCOUNTING_HANDOFF', 'HANDOFF_REFERENCE', 'UNIQUE');
                ensure_index('UX_HR_ADJ_SOURCE', 'HR_PAYROLL_ADJUSTMENT', 'SOURCE_TYPE,SOURCE_ID', 'UNIQUE');
                ensure_index('IX_HR_SHORT_LEAVE_STATUS', 'HR_SHORT_LEAVE_REQUEST', 'STATUS', 'NONUNIQUE');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // COMPANY_ID intentionally remains absent: tenant schema is the company boundary.
    }
}
