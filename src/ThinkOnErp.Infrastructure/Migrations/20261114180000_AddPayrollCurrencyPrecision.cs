using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114180000_AddPayrollCurrencyPrecision")]
public sealed class AddPayrollCurrencyPrecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_column_count NUMBER;
                primary_key_count NUMBER;
                primary_key_columns VARCHAR2(4000);
                check_count NUMBER;
                constraint_collision_count NUMBER;
                index_collision_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_PAYROLL_CURRENCY_PRECISION';

                IF table_count = 0 THEN
                    SELECT COUNT(*) INTO constraint_collision_count
                    FROM ALL_CONSTRAINTS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND CONSTRAINT_NAME IN ('PK_HR_PAY_CURRENCY_PRECISION', 'CK_HR_PAY_CURRENCY_DECIMALS');
                    SELECT COUNT(*) INTO index_collision_count
                    FROM ALL_INDEXES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND INDEX_NAME = 'PK_HR_PAY_CURRENCY_PRECISION';
                    IF constraint_collision_count <> 0 OR index_collision_count <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20098,
                            'HR payroll currency precision constraint or index names are already in use.');
                    END IF;

                    EXECUTE IMMEDIATE '
                        CREATE TABLE HR_PAYROLL_CURRENCY_PRECISION
                        (
                            COMPANY_ID NUMBER(19) NOT NULL,
                            CURRENCY_CODE VARCHAR2(10 BYTE) NOT NULL,
                            DECIMAL_PLACES NUMBER(2) NOT NULL,
                            CREATION_USER VARCHAR2(100 BYTE) NOT NULL,
                            CREATION_DATE TIMESTAMP(7) NOT NULL,
                            UPDATE_USER VARCHAR2(100 BYTE) NULL,
                            UPDATE_DATE TIMESTAMP(7) NULL,
                            CONSTRAINT PK_HR_PAY_CURRENCY_PRECISION PRIMARY KEY (COMPANY_ID, CURRENCY_CODE),
                            CONSTRAINT CK_HR_PAY_CURRENCY_DECIMALS CHECK (DECIMAL_PLACES BETWEEN 0 AND 4)
                        )';
                ELSE
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_CURRENCY_PRECISION';

                    SELECT COUNT(*) INTO compatible_column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_CURRENCY_PRECISION'
                      AND DATA_DEFAULT IS NULL
                      AND VIRTUAL_COLUMN = 'NO'
                      AND (
                          (COLUMN_NAME = 'COMPANY_ID' AND DATA_TYPE = 'NUMBER' AND DATA_PRECISION = 19 AND NVL(DATA_SCALE,0) = 0 AND NULLABLE = 'N') OR
                          (COLUMN_NAME = 'CURRENCY_CODE' AND DATA_TYPE = 'VARCHAR2' AND DATA_LENGTH = 10 AND CHAR_USED = 'B' AND NULLABLE = 'N') OR
                          (COLUMN_NAME = 'DECIMAL_PLACES' AND DATA_TYPE = 'NUMBER' AND DATA_PRECISION = 2 AND NVL(DATA_SCALE,0) = 0 AND NULLABLE = 'N') OR
                          (COLUMN_NAME = 'CREATION_USER' AND DATA_TYPE = 'VARCHAR2' AND DATA_LENGTH = 100 AND CHAR_USED = 'B' AND NULLABLE = 'N') OR
                          (COLUMN_NAME = 'CREATION_DATE' AND DATA_TYPE = 'TIMESTAMP(7)' AND NULLABLE = 'N') OR
                          (COLUMN_NAME = 'UPDATE_USER' AND DATA_TYPE = 'VARCHAR2' AND DATA_LENGTH = 100 AND CHAR_USED = 'B' AND NULLABLE = 'Y') OR
                          (COLUMN_NAME = 'UPDATE_DATE' AND DATA_TYPE = 'TIMESTAMP(7)' AND NULLABLE = 'Y')
                      );

                    IF column_count <> 7 OR compatible_column_count <> 7 THEN
                        RAISE_APPLICATION_ERROR(-20096,
                            'HR_PAYROLL_CURRENCY_PRECISION has an incompatible column definition.');
                    END IF;

                    SELECT COUNT(*) INTO primary_key_count
                    FROM ALL_CONSTRAINTS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_CURRENCY_PRECISION'
                      AND CONSTRAINT_NAME = 'PK_HR_PAY_CURRENCY_PRECISION'
                      AND CONSTRAINT_TYPE = 'P' AND STATUS = 'ENABLED' AND VALIDATED = 'VALIDATED';

                    SELECT LISTAGG(COLUMN_NAME, ',') WITHIN GROUP (ORDER BY POSITION)
                    INTO primary_key_columns
                    FROM ALL_CONS_COLUMNS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_CURRENCY_PRECISION'
                      AND CONSTRAINT_NAME = 'PK_HR_PAY_CURRENCY_PRECISION';

                    SELECT COUNT(*) INTO check_count
                    FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND c.TABLE_NAME = 'HR_PAYROLL_CURRENCY_PRECISION'
                      AND c.CONSTRAINT_NAME = 'CK_HR_PAY_CURRENCY_DECIMALS'
                      AND c.CONSTRAINT_TYPE = 'C' AND c.STATUS = 'ENABLED' AND c.VALIDATED = 'VALIDATED'
                      AND REPLACE(REPLACE(REPLACE(UPPER(c.SEARCH_CONDITION_VC), ' ', ''), '(', ''), ')', '')
                          = 'DECIMAL_PLACESBETWEEN0AND4';

                    IF primary_key_count <> 1 OR primary_key_columns <> 'COMPANY_ID,CURRENCY_CODE' OR check_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20097,
                            'HR_PAYROLL_CURRENCY_PRECISION is missing its validated company/currency key or decimal range constraint.');
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Company-specific precision settings may contain approved business data.
        // Reverting the migration record must not drop the table or those values.
    }
}
