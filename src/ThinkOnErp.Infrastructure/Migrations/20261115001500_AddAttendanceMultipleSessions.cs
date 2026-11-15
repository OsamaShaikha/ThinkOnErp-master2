using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001500_AddAttendanceMultipleSessions")]
public sealed class AddAttendanceMultipleSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                invalid_value_count NUMBER;
                actual_type VARCHAR2(128);
                actual_precision NUMBER;
                actual_scale NUMBER;
                actual_nullable VARCHAR2(1);
                actual_default_length NUMBER;
                actual_default VARCHAR2(4000);
                actual_virtual VARCHAR2(3);
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_ATTENDANCE_POLICY';
                IF table_count = 0 THEN
                    RETURN;
                END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_ATTENDANCE_POLICY'
                  AND COLUMN_NAME = 'ALLOW_MULTIPLE_SESSIONS';
                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_ATTENDANCE_POLICY" ADD ("ALLOW_MULTIPLE_SESSIONS" NUMBER(1) DEFAULT 0 NOT NULL)';
                    RETURN;
                END IF;

                IF column_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20230, 'HR_ATTENDANCE_POLICY.ALLOW_MULTIPLE_SESSIONS metadata is ambiguous.');
                END IF;

                SELECT DATA_TYPE, DATA_PRECISION, DATA_SCALE, NULLABLE,
                       NVL(DEFAULT_LENGTH, 0), DATA_DEFAULT, VIRTUAL_COLUMN
                INTO actual_type, actual_precision, actual_scale, actual_nullable,
                     actual_default_length, actual_default, actual_virtual
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_ATTENDANCE_POLICY'
                  AND COLUMN_NAME = 'ALLOW_MULTIPLE_SESSIONS';

                IF actual_type = 'NUMBER'
                   AND actual_precision = 1
                   AND actual_scale = 0
                   AND actual_nullable = 'N'
                   AND actual_default_length > 0
                   AND REGEXP_REPLACE(UPPER(actual_default), '[^A-Z0-9]', '') = '0'
                   AND actual_virtual = 'NO' THEN
                    NULL;
                ELSE
                    RAISE_APPLICATION_ERROR(-20231, 'HR_ATTENDANCE_POLICY.ALLOW_MULTIPLE_SESSIONS has an incompatible definition.');
                END IF;

                EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_ATTENDANCE_POLICY" WHERE "ALLOW_MULTIPLE_SESSIONS" IS NULL OR "ALLOW_MULTIPLE_SESSIONS" NOT IN (0,1)'
                    INTO invalid_value_count;
                IF invalid_value_count <> 0 THEN
                    RAISE_APPLICATION_ERROR(-20232, 'HR_ATTENDANCE_POLICY.ALLOW_MULTIPLE_SESSIONS contains values outside the boolean range.');
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the attendance setting and its configured values on rollback.
    }
}
