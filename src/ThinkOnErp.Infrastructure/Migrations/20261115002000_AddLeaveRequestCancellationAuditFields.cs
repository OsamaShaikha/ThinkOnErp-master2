using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115002000_AddLeaveRequestCancellationAuditFields")]
public sealed class AddLeaveRequestCancellationAuditFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE inspect_column(p_column VARCHAR2, p_kind VARCHAR2, p_length NUMBER DEFAULT NULL) IS
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_REQUEST' AND COLUMN_NAME=p_column;
                    IF column_count=0 THEN RETURN; END IF;

                    BEGIN
                        EXECUTE IMMEDIATE
                            'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=:owner AND TABLE_NAME=''HR_LEAVE_REQUEST'' AND COLUMN_NAME=:column_name AND NULLABLE=''Y'' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN=''NO'' AND ((:kind=''TEXT'' AND DATA_TYPE=''NVARCHAR2'' AND CHAR_LENGTH>=:column_length) OR (:kind=''TIMESTAMP'' AND DATA_TYPE=''TIMESTAMP(7)''))'
                            INTO compatible_count USING owner_name, p_column, p_kind, p_length, p_kind;
                    EXCEPTION WHEN OTHERS THEN
                        IF SQLCODE != -904 THEN RAISE; END IF;
                        -- Older Oracle releases cannot define virtual columns and omit this dictionary field.
                        EXECUTE IMMEDIATE
                            'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=:owner AND TABLE_NAME=''HR_LEAVE_REQUEST'' AND COLUMN_NAME=:column_name AND NULLABLE=''Y'' AND NVL(DEFAULT_LENGTH,0)=0 AND ((:kind=''TEXT'' AND DATA_TYPE=''NVARCHAR2'' AND CHAR_LENGTH>=:column_length) OR (:kind=''TIMESTAMP'' AND DATA_TYPE=''TIMESTAMP(7)''))'
                            INTO compatible_count USING owner_name, p_column, p_kind, p_length, p_kind;
                    END;
                    IF compatible_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20320, 'HR_LEAVE_REQUEST.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;
            BEGIN
                SELECT COUNT(*) INTO table_count FROM ALL_TABLES
                WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_REQUEST';

                -- Bootstrap creates a wholly missing request table from the current EF model.
                IF table_count=1 THEN
                    inspect_column('CANCELLED_BY','TEXT',100);
                    inspect_column('CANCELLATION_DATE','TIMESTAMP');
                    inspect_column('CANCELLATION_REASON','TEXT',500);

                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_REQUEST' AND COLUMN_NAME='CANCELLED_BY';
                    IF column_count=0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_REQUEST" ADD ("CANCELLED_BY" NVARCHAR2(100) NULL)';
                    END IF;

                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_REQUEST' AND COLUMN_NAME='CANCELLATION_DATE';
                    IF column_count=0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_REQUEST" ADD ("CANCELLATION_DATE" TIMESTAMP(7) NULL)';
                    END IF;

                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_REQUEST' AND COLUMN_NAME='CANCELLATION_REASON';
                    IF column_count=0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_REQUEST" ADD ("CANCELLATION_REASON" NVARCHAR2(500) NULL)';
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve cancellation audit fields when rolling back application code.
    }
}
