using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000300_AddAttendanceSnapshotOptionalColumns")]
public sealed class AddAttendanceSnapshotOptionalColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE inspect_text(p_table VARCHAR2, p_column VARCHAR2, p_length NUMBER) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                      AND DATA_TYPE = 'NVARCHAR2' AND CHAR_LENGTH = p_length
                      AND NULLABLE = 'Y' AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20181, p_table || '.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE inspect_timestamp(p_table VARCHAR2, p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                      AND DATA_TYPE = 'TIMESTAMP(7)' AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0 AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20182, p_table || '.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE inspect_minutes IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_ATTENDANCE_DAY';
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_ATTENDANCE_DAY' AND COLUMN_NAME = 'SHIFT_BREAK_MINUTES';
                    IF column_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_ATTENDANCE_DAY' AND COLUMN_NAME = 'SHIFT_BREAK_MINUTES'
                      AND DATA_TYPE = 'NUMBER' AND DATA_PRECISION = 10 AND NVL(DATA_SCALE, 0) = 0
                      AND NULLABLE = 'Y' AND NVL(DEFAULT_LENGTH, 0) = 0 AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20183, 'HR_ATTENDANCE_DAY.SHIFT_BREAK_MINUTES has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE add_if_missing(p_table VARCHAR2, p_column VARCHAR2, p_definition VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "' || p_table || '" ADD ("' || p_column || '" ' || p_definition || ')';
                    END IF;
                END;
            BEGIN
                -- Preflight all existing columns in both tables before issuing DDL.
                inspect_text('HR_ATTENDANCE_CORRECTION', 'ORIGINAL_DAY_VERSION', 64);
                inspect_timestamp('HR_ATTENDANCE_CORRECTION', 'APPLIED_CHECK_IN');
                inspect_timestamp('HR_ATTENDANCE_CORRECTION', 'APPLIED_CHECK_OUT');
                inspect_text('HR_ATTENDANCE_DAY', 'TIME_ZONE_ID', 100);
                inspect_timestamp('HR_ATTENDANCE_DAY', 'SHIFT_STARTS_AT_UTC');
                inspect_timestamp('HR_ATTENDANCE_DAY', 'SHIFT_ENDS_AT_UTC');
                inspect_minutes();
                inspect_text('HR_ATTENDANCE_DAY', 'SHIFT_SOURCE', 30);

                add_if_missing('HR_ATTENDANCE_CORRECTION', 'ORIGINAL_DAY_VERSION', 'NVARCHAR2(64) NULL');
                add_if_missing('HR_ATTENDANCE_CORRECTION', 'APPLIED_CHECK_IN', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_ATTENDANCE_CORRECTION', 'APPLIED_CHECK_OUT', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_ATTENDANCE_DAY', 'TIME_ZONE_ID', 'NVARCHAR2(100) NULL');
                add_if_missing('HR_ATTENDANCE_DAY', 'SHIFT_STARTS_AT_UTC', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_ATTENDANCE_DAY', 'SHIFT_ENDS_AT_UTC', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_ATTENDANCE_DAY', 'SHIFT_BREAK_MINUTES', 'NUMBER(10) NULL');
                add_if_missing('HR_ATTENDANCE_DAY', 'SHIFT_SOURCE', 'NVARCHAR2(30) NULL');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve attendance snapshots and correction history during rollback.
    }
}
