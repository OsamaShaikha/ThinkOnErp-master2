using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114235800_AddEndOfServiceOptionalColumns")]
public sealed class AddEndOfServiceOptionalColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                actual_type VARCHAR2(128);
                actual_length NUMBER;
                actual_nullable CHAR(1);
                actual_precision NUMBER;
                actual_scale NUMBER;
                actual_default_length NUMBER;
                actual_virtual_column VARCHAR2(3);

                PROCEDURE inspect_column(
                    p_table VARCHAR2,
                    p_column VARCHAR2,
                    p_kind VARCHAR2,
                    p_length NUMBER DEFAULT NULL
                ) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;
                    IF table_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT DATA_TYPE, CHAR_LENGTH, NULLABLE, DATA_PRECISION, DATA_SCALE,
                           NVL(DEFAULT_LENGTH, 0), VIRTUAL_COLUMN
                    INTO actual_type, actual_length, actual_nullable, actual_precision,
                         actual_scale, actual_default_length, actual_virtual_column
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column;

                    IF p_kind = 'TEXT' THEN
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                          AND DATA_TYPE = 'NVARCHAR2' AND CHAR_LENGTH >= p_length
                          AND NULLABLE = 'Y' AND NVL(DEFAULT_LENGTH, 0) = 0
                          AND VIRTUAL_COLUMN = 'NO';
                    ELSIF p_kind = 'NUMBER' THEN
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                          AND DATA_TYPE = 'NUMBER' AND DATA_PRECISION = 19
                          AND NVL(DATA_SCALE, 0) = 0 AND NULLABLE = 'Y'
                          AND NVL(DEFAULT_LENGTH, 0) = 0 AND VIRTUAL_COLUMN = 'NO';
                    ELSE
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
                          AND DATA_TYPE = 'TIMESTAMP(7)' AND NULLABLE = 'Y'
                          AND NVL(DEFAULT_LENGTH, 0) = 0 AND VIRTUAL_COLUMN = 'NO';
                    END IF;

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20141, p_table || '.' || p_column || ' has an incompatible definition (' || actual_type || ').');
                    END IF;
                END;

                PROCEDURE add_if_missing(p_table VARCHAR2, p_column VARCHAR2, p_definition VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;
                    IF table_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "' || p_table || '" ADD ("' || p_column || '" ' || p_definition || ')';
                    END IF;
                END;
            BEGIN
                -- Validate every existing definition before issuing the first DDL statement.
                inspect_column('HR_END_OF_SERVICE_POLICY', 'UPDATE_USER', 'TEXT', 200);
                inspect_column('HR_END_OF_SERVICE_POLICY', 'UPDATE_DATE', 'TIMESTAMP');
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'BRANCH_ID', 'NUMBER');
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'TERMINATION_REASON', 'TEXT', 1000);
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'NOTICE_DATE', 'TIMESTAMP');
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'CALCULATION_NOTES', 'TEXT', 2000);
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'APPROVED_BY', 'TEXT', 200);
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'APPROVED_AT_UTC', 'TIMESTAMP');
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'CANCELLED_BY', 'TEXT', 200);
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'CANCELLED_AT_UTC', 'TIMESTAMP');
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'CANCELLATION_REASON', 'TEXT', 1000);
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'UPDATE_USER', 'TEXT', 200);
                inspect_column('HR_END_OF_SERVICE_SETTLEMENT', 'UPDATE_DATE', 'TIMESTAMP');
                inspect_column('HR_EOS_SETTLEMENT_LINE', 'DESCRIPTION', 'TEXT', 500);

                add_if_missing('HR_END_OF_SERVICE_POLICY', 'UPDATE_USER', 'NVARCHAR2(200) NULL');
                add_if_missing('HR_END_OF_SERVICE_POLICY', 'UPDATE_DATE', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'BRANCH_ID', 'NUMBER(19) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'TERMINATION_REASON', 'NVARCHAR2(1000) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'NOTICE_DATE', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'CALCULATION_NOTES', 'NVARCHAR2(2000) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'APPROVED_BY', 'NVARCHAR2(200) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'APPROVED_AT_UTC', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'CANCELLED_BY', 'NVARCHAR2(200) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'CANCELLED_AT_UTC', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'CANCELLATION_REASON', 'NVARCHAR2(1000) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'UPDATE_USER', 'NVARCHAR2(200) NULL');
                add_if_missing('HR_END_OF_SERVICE_SETTLEMENT', 'UPDATE_DATE', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_EOS_SETTLEMENT_LINE', 'DESCRIPTION', 'NVARCHAR2(500) NULL');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve optional historical settlement details and audit metadata.
    }
}
