using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000500_AddLeaveClosePolicySnapshotColumns")]
public sealed class AddLeaveClosePolicySnapshotColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE inspect_number(p_column VARCHAR2, p_precision NUMBER, p_scale NUMBER) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE';
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE'
                      AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE'
                      AND COLUMN_NAME = p_column
                      AND DATA_TYPE = 'NUMBER'
                      AND DATA_PRECISION = p_precision
                      AND NVL(DATA_SCALE, 0) = p_scale
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20201, 'HR_LEAVE_YEAR_CLOSE_LINE.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE inspect_action IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE';
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE'
                      AND COLUMN_NAME = 'YEAR_END_EXCESS_ACTION_SNAPSHOT';
                    IF column_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE'
                      AND COLUMN_NAME = 'YEAR_END_EXCESS_ACTION_SNAPSHOT'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH >= 20
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20202, 'HR_LEAVE_YEAR_CLOSE_LINE.YEAR_END_EXCESS_ACTION_SNAPSHOT has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE add_if_missing(p_column VARCHAR2, p_definition VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE';
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE_LINE'
                      AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_YEAR_CLOSE_LINE" ADD ("' || p_column || '" ' || p_definition || ')';
                    END IF;
                END;
            BEGIN
                -- Inspect every existing snapshot column before the first DDL statement.
                inspect_number('TRACKS_BALANCE_SNAPSHOT', 1, 0);
                inspect_number('CARRY_FORWARD_ALLOWED_SNAPSHOT', 1, 0);
                inspect_number('CARRY_FORWARD_CAP_DAYS_SNAPSHOT', 6, 2);
                inspect_action();

                add_if_missing('TRACKS_BALANCE_SNAPSHOT', 'NUMBER(1) NULL');
                add_if_missing('CARRY_FORWARD_ALLOWED_SNAPSHOT', 'NUMBER(1) NULL');
                add_if_missing('CARRY_FORWARD_CAP_DAYS_SNAPSHOT', 'NUMBER(6,2) NULL');
                add_if_missing('YEAR_END_EXCESS_ACTION_SNAPSHOT', 'NVARCHAR2(20) NULL');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve the historical leave-year-close policy snapshots during rollback.
    }
}
