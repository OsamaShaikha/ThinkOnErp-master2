using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114220000_AddLoanAdvanceCurrencyCodes")]
public sealed class AddLoanAdvanceCurrencyCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE preflight_currency_column(p_table VARCHAR2) IS
                    target_table_count NUMBER;
                    existing_column_count NUMBER;
                    valid_column_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO target_table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;
                    IF target_table_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20101, p_table || ' must exist in the current schema.');
                    END IF;

                    SELECT COUNT(*) INTO existing_column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = 'CURRENCY_CODE';
                    IF existing_column_count > 1 THEN
                        RAISE_APPLICATION_ERROR(-20103, p_table || '.CURRENCY_CODE metadata is ambiguous.');
                    ELSIF existing_column_count = 1 THEN
                        SELECT COUNT(*) INTO valid_column_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = p_table
                          AND COLUMN_NAME = 'CURRENCY_CODE'
                          AND DATA_TYPE = 'NVARCHAR2'
                          AND CHAR_LENGTH >= 20
                          AND NULLABLE = 'Y'
                          AND NVL(DEFAULT_LENGTH, 0) = 0
                          AND VIRTUAL_COLUMN = 'NO';
                        IF valid_column_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20102, p_table || '.CURRENCY_CODE has an incompatible definition.');
                        END IF;
                    END IF;
                END;

                PROCEDURE ensure_currency_column(p_table VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;

                    IF table_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20101, p_table || ' must exist in the current schema.');
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = 'CURRENCY_CODE';

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "' || p_table || '" ADD ("CURRENCY_CODE" NVARCHAR2(20) NULL)';
                    END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = 'CURRENCY_CODE'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH >= 20
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20102, p_table || '.CURRENCY_CODE has an incompatible definition.');
                    END IF;
                END;
            BEGIN
                -- Preflight both targets before Oracle's first implicit DDL commit.
                preflight_currency_column('HR_EMPLOYEE_LOAN');
                preflight_currency_column('HR_EMPLOYEE_ADVANCE');

                ensure_currency_column('HR_EMPLOYEE_LOAN');
                ensure_currency_column('HR_EMPLOYEE_ADVANCE');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve explicit and historical currency values when rolling back.
    }
}
