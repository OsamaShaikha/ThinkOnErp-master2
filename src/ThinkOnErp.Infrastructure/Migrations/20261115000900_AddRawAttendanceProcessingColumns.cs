using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000900_AddRawAttendanceProcessingColumns")]
public sealed class AddRawAttendanceProcessingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE inspect_column(p_column VARCHAR2, p_length NUMBER) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_RAW_ATTENDANCE';
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_RAW_ATTENDANCE'
                      AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_RAW_ATTENDANCE'
                      AND COLUMN_NAME = p_column
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = p_length
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20184, 'HR_RAW_ATTENDANCE.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE add_if_missing(p_column VARCHAR2, p_length NUMBER) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_RAW_ATTENDANCE';
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_RAW_ATTENDANCE'
                      AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_RAW_ATTENDANCE" ADD ("' || p_column || '" NVARCHAR2(' || TO_CHAR(p_length, 'FM9990') || ') NULL)';
                    END IF;
                END;
            BEGIN
                -- Validate every existing definition before any DDL starts.
                inspect_column('EVENT_ID', 100);
                inspect_column('EVENT_PAYLOAD_HASH', 64);
                inspect_column('LAST_ATTEMPTED_BY', 200);
                inspect_column('PROCESSED_BY', 200);
                inspect_column('PROCESSING_OUTCOME', 20);
                inspect_column('PROCESSING_REASON', 1000);

                add_if_missing('EVENT_ID', 100);
                add_if_missing('EVENT_PAYLOAD_HASH', 64);
                add_if_missing('LAST_ATTEMPTED_BY', 200);
                add_if_missing('PROCESSED_BY', 200);
                add_if_missing('PROCESSING_OUTCOME', 20);
                add_if_missing('PROCESSING_REASON', 1000);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep device event identities and attendance processing history on rollback.
    }
}
