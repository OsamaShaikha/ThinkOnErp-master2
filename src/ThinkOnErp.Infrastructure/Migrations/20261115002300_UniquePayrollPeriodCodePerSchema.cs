using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115002300_UniquePayrollPeriodCodePerSchema")]
public sealed class UniquePayrollPeriodCodePerSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                table_count NUMBER;
                duplicate_count NUMBER;
                index_count NUMBER;
                compatible_index_count NUMBER;
                index_columns VARCHAR2(4000);
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_PAYROLL_PERIOD';
                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20340, 'HR_PAYROLL_PERIOD is required before its period code key can be installed.');
                END IF;

                SELECT COUNT(*) INTO table_count
                FROM ALL_TAB_COLS
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_PAYROLL_PERIOD'
                  AND VIRTUAL_COLUMN = 'NO' AND NULLABLE = 'N'
                  AND COLUMN_NAME = 'PERIOD_CODE'
                  AND DATA_TYPE = 'NVARCHAR2' AND CHAR_LENGTH = 20;
                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20341, 'HR_PAYROLL_PERIOD.PERIOD_CODE is missing or incompatible; no DDL was applied.');
                END IF;

                SELECT COUNT(*) INTO duplicate_count
                FROM (
                    SELECT PERIOD_CODE
                    FROM HR_PAYROLL_PERIOD
                    GROUP BY PERIOD_CODE
                    HAVING COUNT(*) > 1
                );
                IF duplicate_count <> 0 THEN
                    RAISE_APPLICATION_ERROR(-20342,
                        'Duplicate payroll period codes exist in this schema; resolve them before installing the unique key. No DDL was applied.');
                END IF;

                SELECT COUNT(*) INTO index_count
                FROM ALL_INDEXES
                WHERE OWNER = owner_name AND INDEX_NAME = 'UX_HR_PAY_PERIOD_CODE';

                IF index_count = 0 THEN
                    EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UX_HR_PAY_PERIOD_CODE ON HR_PAYROLL_PERIOD (PERIOD_CODE)';
                ELSE
                    SELECT COUNT(*) INTO compatible_index_count
                    FROM ALL_INDEXES
                    WHERE OWNER = owner_name
                      AND INDEX_NAME = 'UX_HR_PAY_PERIOD_CODE'
                      AND TABLE_NAME = 'HR_PAYROLL_PERIOD'
                      AND UNIQUENESS = 'UNIQUE'
                      AND STATUS = 'VALID';

                    SELECT LISTAGG(COLUMN_NAME, ',') WITHIN GROUP (ORDER BY COLUMN_POSITION)
                    INTO index_columns
                    FROM ALL_IND_COLUMNS
                    WHERE INDEX_OWNER = owner_name
                      AND INDEX_NAME = 'UX_HR_PAY_PERIOD_CODE';

                    IF compatible_index_count <> 1 OR index_columns <> 'PERIOD_CODE' THEN
                        RAISE_APPLICATION_ERROR(-20343,
                            'UX_HR_PAY_PERIOD_CODE is already used by an incompatible index; no DDL was applied.');
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Payroll period identity is business data; keep its uniqueness protection on rollback.
    }
}
