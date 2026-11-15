using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000200_AddLeaveSettingsOptionalColumns")]
public sealed class AddLeaveSettingsOptionalColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE inspect_text(p_column VARCHAR2, p_length NUMBER) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS';

                    IF table_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                      AND COLUMN_NAME = p_column;

                    IF column_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                      AND COLUMN_NAME = p_column
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = p_length
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20171, 'HR_LEAVE_SETTINGS.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE inspect_divisor IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS';

                    IF table_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                      AND COLUMN_NAME = 'LEAVE_CASHOUT_DAY_DIVISOR';

                    IF column_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                      AND COLUMN_NAME = 'LEAVE_CASHOUT_DAY_DIVISOR'
                      AND DATA_TYPE = 'NUMBER'
                      AND DATA_PRECISION = 10
                      AND DATA_SCALE = 4
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20172, 'HR_LEAVE_SETTINGS.LEAVE_CASHOUT_DAY_DIVISOR has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE add_if_missing(p_column VARCHAR2, p_definition VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS';

                    IF table_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                      AND COLUMN_NAME = p_column;

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_SETTINGS" ADD ("' || p_column || '" ' || p_definition || ')';
                    END IF;
                END;
            BEGIN
                -- Preflight every present definition before the first DDL statement.
                inspect_text('ACCRUAL_TIME_ZONE_ID', 120);
                inspect_text('LEAVE_CASHOUT_WAGE_BASIS_CODE', 30);
                inspect_text('LEAVE_CASHOUT_BASIS_COMPONENT_CODE', 100);
                inspect_divisor();
                inspect_text('LEAVE_CASHOUT_PAY_COMPONENT_CODE', 100);

                add_if_missing('ACCRUAL_TIME_ZONE_ID', 'NVARCHAR2(120) NULL');
                add_if_missing('LEAVE_CASHOUT_WAGE_BASIS_CODE', 'NVARCHAR2(30) NULL');
                add_if_missing('LEAVE_CASHOUT_BASIS_COMPONENT_CODE', 'NVARCHAR2(100) NULL');
                add_if_missing('LEAVE_CASHOUT_DAY_DIVISOR', 'NUMBER(10,4) NULL');
                add_if_missing('LEAVE_CASHOUT_PAY_COMPONENT_CODE', 'NVARCHAR2(100) NULL');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve tenant leave configuration during rollback.
    }
}
