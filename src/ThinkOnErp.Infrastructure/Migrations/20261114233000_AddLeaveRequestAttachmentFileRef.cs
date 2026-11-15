using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114233000_AddLeaveRequestAttachmentFileRef")]
public sealed class AddLeaveRequestAttachmentFileRef : Migration
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
                  AND TABLE_NAME = 'HR_LEAVE_REQUEST';

                -- A missing table is created from the current EF model by tenant bootstrap.
                IF table_count = 1 THEN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_REQUEST'
                      AND COLUMN_NAME = 'ATTACHMENT_FILE_REF';

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_REQUEST" ADD ("ATTACHMENT_FILE_REF" NVARCHAR2(500) NULL)';
                    END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_REQUEST'
                      AND COLUMN_NAME = 'ATTACHMENT_FILE_REF'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH >= 500
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20121, 'HR_LEAVE_REQUEST.ATTACHMENT_FILE_REF has an incompatible definition.');
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve tenant attachment references on rollback.
    }
}
