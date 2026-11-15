using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001000_AddLeaveYearCloseUnpostedBy")]
public sealed class AddLeaveYearCloseUnpostedBy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE';
                IF table_count = 0 THEN RETURN; END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE'
                  AND COLUMN_NAME = 'UNPOSTED_BY';

                IF column_count > 0 THEN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE'
                      AND COLUMN_NAME = 'UNPOSTED_BY'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = 200
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20185, 'HR_LEAVE_YEAR_CLOSE.UNPOSTED_BY has an incompatible definition.');
                    END IF;
                    RETURN;
                END IF;

                EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_YEAR_CLOSE" ADD ("UNPOSTED_BY" NVARCHAR2(200) NULL)';
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve the leave close audit trail during rollback.
    }
}
