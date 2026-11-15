using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114235700_AddShortLeaveOptionalColumns")]
public sealed class AddShortLeaveOptionalColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                actual_type VARCHAR2(128);

                PROCEDURE inspect_column(p_table VARCHAR2, p_column VARCHAR2, p_kind VARCHAR2, p_length NUMBER DEFAULT NULL) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table;
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN RETURN; END IF;

                    SELECT DATA_TYPE INTO actual_type
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table AND COLUMN_NAME = p_column;

                    IF p_kind = 'TEXT' THEN
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                          AND DATA_TYPE = 'NVARCHAR2' AND CHAR_LENGTH >= p_length AND NULLABLE = 'Y'
                          AND NVL(DEFAULT_LENGTH, 0) = 0 AND VIRTUAL_COLUMN = 'NO';
                    ELSIF p_kind = 'NUMBER' THEN
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                          AND DATA_TYPE = 'NUMBER' AND DATA_PRECISION = 19 AND NVL(DATA_SCALE, 0) = 0
                          AND NULLABLE = 'Y' AND NVL(DEFAULT_LENGTH, 0) = 0 AND VIRTUAL_COLUMN = 'NO';
                    ELSE
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                          AND DATA_TYPE = 'TIMESTAMP(7)' AND NULLABLE = 'Y'
                          AND NVL(DEFAULT_LENGTH, 0) = 0 AND VIRTUAL_COLUMN = 'NO';
                    END IF;

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20151, p_table || '.' || p_column || ' has an incompatible definition (' || actual_type || ').');
                    END IF;
                END;

                PROCEDURE add_if_missing(p_table VARCHAR2, p_column VARCHAR2, p_definition VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table;
                    IF table_count = 0 THEN RETURN; END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = p_table AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "' || p_table || '" ADD ("' || p_column || '" ' || p_definition || ')';
                    END IF;
                END;
            BEGIN
                -- Preflight all present optional columns before the first DDL statement.
                inspect_column('HR_SHORT_LEAVE_POLICY', 'UPDATE_USER', 'TEXT', 100);
                inspect_column('HR_SHORT_LEAVE_POLICY', 'UPDATE_DATE', 'TIMESTAMP');
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'BRANCH_ID', 'NUMBER');
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'APPROVED_BY', 'TEXT', 100);
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'APPROVED_AT_UTC', 'TIMESTAMP');
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'REJECTED_BY', 'TEXT', 100);
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'REJECTED_AT_UTC', 'TIMESTAMP');
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'REJECTION_REASON', 'TEXT', 500);
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'CANCELLED_BY', 'TEXT', 100);
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'CANCELLED_AT_UTC', 'TIMESTAMP');
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'CANCELLATION_REASON', 'TEXT', 500);
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'UPDATE_USER', 'TEXT', 100);
                inspect_column('HR_SHORT_LEAVE_REQUEST', 'UPDATE_DATE', 'TIMESTAMP');

                add_if_missing('HR_SHORT_LEAVE_POLICY', 'UPDATE_USER', 'NVARCHAR2(100) NULL');
                add_if_missing('HR_SHORT_LEAVE_POLICY', 'UPDATE_DATE', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'BRANCH_ID', 'NUMBER(19) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'APPROVED_BY', 'NVARCHAR2(100) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'APPROVED_AT_UTC', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'REJECTED_BY', 'NVARCHAR2(100) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'REJECTED_AT_UTC', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'REJECTION_REASON', 'NVARCHAR2(500) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'CANCELLED_BY', 'NVARCHAR2(100) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'CANCELLED_AT_UTC', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'CANCELLATION_REASON', 'NVARCHAR2(500) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'UPDATE_USER', 'NVARCHAR2(100) NULL');
                add_if_missing('HR_SHORT_LEAVE_REQUEST', 'UPDATE_DATE', 'TIMESTAMP(7) NULL');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve historical workflow and audit metadata.
    }
}
