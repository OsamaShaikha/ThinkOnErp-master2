using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114160000_AddPayrollAdjustmentDecision")]
public sealed class AddPayrollAdjustmentDecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep tenant migrations compatible with the standalone schema tool.
        // The snapshot columns are nullable and intentionally have no defaults.
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                index_count NUMBER;
                unique_index_count NUMBER;
                valid_index_count NUMBER;
                index_columns VARCHAR2(4000);
                duplicate_count NUMBER;
                current_schema_name VARCHAR2(128);
                source_column_count NUMBER;

                PROCEDURE preflight_column(
                    p_name IN VARCHAR2,
                    p_type IN VARCHAR2,
                    p_length IN NUMBER,
                    p_precision IN NUMBER,
                    p_scale IN NUMBER) IS
                    column_count NUMBER;
                    compatible_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND COLUMN_NAME = p_name;

                    IF column_count > 1 THEN
                        RAISE_APPLICATION_ERROR(-20094,
                            'HR_PAYROLL_ADJUSTMENT.' || p_name || ' metadata is ambiguous.');
                    ELSIF column_count = 1 THEN
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                          AND COLUMN_NAME = p_name
                          AND DATA_TYPE = p_type
                          AND NULLABLE = 'Y'
                          AND DATA_DEFAULT IS NULL
                          AND VIRTUAL_COLUMN = 'NO'
                          AND (p_length = 0 OR CHAR_LENGTH >= p_length)
                          AND (p_precision = 0 OR
                               (DATA_PRECISION = p_precision AND NVL(DATA_SCALE, 0) = p_scale));

                        IF compatible_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20091,
                                'HR_PAYROLL_ADJUSTMENT.' || p_name || ' has an incompatible definition.');
                        END IF;
                    END IF;
                END;

                PROCEDURE ensure_column(
                    p_name IN VARCHAR2,
                    p_type IN VARCHAR2,
                    p_length IN NUMBER,
                    p_precision IN NUMBER,
                    p_scale IN NUMBER,
                    p_definition IN VARCHAR2) IS
                    column_count NUMBER;
                    compatible_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND COLUMN_NAME = p_name;

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE
                            'ALTER TABLE HR_PAYROLL_ADJUSTMENT ADD (' || p_name || ' ' || p_definition || ' NULL)';
                    ELSE
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                          AND COLUMN_NAME = p_name
                          AND DATA_TYPE = p_type
                          AND NULLABLE = 'Y'
                          AND DATA_DEFAULT IS NULL
                          AND VIRTUAL_COLUMN = 'NO'
                          AND (p_length = 0 OR CHAR_LENGTH >= p_length)
                          AND (p_precision = 0 OR
                               (DATA_PRECISION = p_precision AND NVL(DATA_SCALE, 0) = p_scale));

                        IF compatible_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20091,
                                'HR_PAYROLL_ADJUSTMENT.' || p_name || ' has an incompatible definition.');
                        END IF;
                    END IF;
                END;
            BEGIN
                current_schema_name := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = current_schema_name
                  AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT';

                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20090,
                        'HR_PAYROLL_ADJUSTMENT must exist in the current schema.');
                END IF;

                -- Oracle commits DDL implicitly. Validate every existing column,
                -- the source index, and duplicate source keys before adding any column.
                preflight_column('DECISION_REASON', 'NVARCHAR2', 1000, 0, 0);
                preflight_column('SOURCE_TYPE', 'NVARCHAR2', 40, 0, 0);
                preflight_column('SOURCE_ID', 'NVARCHAR2', 100, 0, 0);
                preflight_column('SOURCE_BRANCH_ID', 'NUMBER', 0, 19, 0);
                preflight_column('SOURCE_ATTENDANCE_DATE', 'DATE', 0, 0, 0);
                preflight_column('SOURCE_OVERTIME_HOURS', 'NUMBER', 0, 12, 4);
                preflight_column('SOURCE_BASE_SALARY', 'NUMBER', 0, 18, 4);
                preflight_column('SOURCE_DAY_TYPE', 'NVARCHAR2', 50, 0, 0);
                preflight_column('SOURCE_CURRENCY_CODE', 'NVARCHAR2', 20, 0, 0);

                SELECT COUNT(*) INTO index_count
                FROM ALL_INDEXES
                WHERE OWNER = current_schema_name
                  AND INDEX_NAME = 'UX_HR_ADJ_SOURCE';

                IF index_count > 1 THEN
                    RAISE_APPLICATION_ERROR(-20093, 'UX_HR_ADJ_SOURCE has ambiguous metadata.');
                ELSIF index_count = 1 THEN
                    SELECT COUNT(*) INTO unique_index_count
                    FROM ALL_INDEXES
                    WHERE OWNER = current_schema_name
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND INDEX_NAME = 'UX_HR_ADJ_SOURCE'
                      AND UNIQUENESS = 'UNIQUE';

                    SELECT COUNT(*) INTO valid_index_count
                    FROM ALL_INDEXES
                    WHERE OWNER = current_schema_name
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND INDEX_NAME = 'UX_HR_ADJ_SOURCE'
                      AND STATUS = 'VALID';

                    SELECT LISTAGG(COLUMN_NAME, ',') WITHIN GROUP (ORDER BY COLUMN_POSITION)
                    INTO index_columns
                    FROM ALL_IND_COLUMNS
                    WHERE INDEX_OWNER = current_schema_name
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND INDEX_NAME = 'UX_HR_ADJ_SOURCE';

                    IF unique_index_count <> 1 OR valid_index_count <> 1 OR
                       index_columns <> 'COMPANY_ID,SOURCE_TYPE,SOURCE_ID' THEN
                        RAISE_APPLICATION_ERROR(-20093,
                            'UX_HR_ADJ_SOURCE has an incompatible definition.');
                    END IF;
                ELSE
                    SELECT COUNT(*) INTO source_column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = current_schema_name
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND COLUMN_NAME IN ('SOURCE_TYPE', 'SOURCE_ID');

                    IF source_column_count = 2 THEN
                        SELECT COUNT(*) INTO duplicate_count
                        FROM (
                            SELECT COMPANY_ID, SOURCE_TYPE, SOURCE_ID
                            FROM HR_PAYROLL_ADJUSTMENT
                            WHERE SOURCE_TYPE IS NOT NULL AND SOURCE_ID IS NOT NULL
                            GROUP BY COMPANY_ID, SOURCE_TYPE, SOURCE_ID
                            HAVING COUNT(*) > 1
                        );

                        IF duplicate_count <> 0 THEN
                            RAISE_APPLICATION_ERROR(-20092,
                                'Duplicate payroll adjustment source links must be reviewed before adding UX_HR_ADJ_SOURCE.');
                        END IF;
                    END IF;
                END IF;

                ensure_column('DECISION_REASON', 'NVARCHAR2', 1000, 0, 0, 'NVARCHAR2(1000)');
                ensure_column('SOURCE_TYPE', 'NVARCHAR2', 40, 0, 0, 'NVARCHAR2(40)');
                ensure_column('SOURCE_ID', 'NVARCHAR2', 100, 0, 0, 'NVARCHAR2(100)');
                ensure_column('SOURCE_BRANCH_ID', 'NUMBER', 0, 19, 0, 'NUMBER(19)');
                ensure_column('SOURCE_ATTENDANCE_DATE', 'DATE', 0, 0, 0, 'DATE');
                ensure_column('SOURCE_OVERTIME_HOURS', 'NUMBER', 0, 12, 4, 'NUMBER(12,4)');
                ensure_column('SOURCE_BASE_SALARY', 'NUMBER', 0, 18, 4, 'NUMBER(18,4)');
                ensure_column('SOURCE_DAY_TYPE', 'NVARCHAR2', 50, 0, 0, 'NVARCHAR2(50)');
                ensure_column('SOURCE_CURRENCY_CODE', 'NVARCHAR2', 20, 0, 0, 'NVARCHAR2(20)');

                SELECT COUNT(*) INTO index_count
                FROM ALL_INDEXES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND INDEX_NAME = 'UX_HR_ADJ_SOURCE';

                IF index_count = 0 THEN
                    SELECT COUNT(*) INTO duplicate_count
                    FROM (
                        SELECT COMPANY_ID, SOURCE_TYPE, SOURCE_ID
                        FROM HR_PAYROLL_ADJUSTMENT
                        WHERE SOURCE_TYPE IS NOT NULL AND SOURCE_ID IS NOT NULL
                        GROUP BY COMPANY_ID, SOURCE_TYPE, SOURCE_ID
                        HAVING COUNT(*) > 1
                    );

                    IF duplicate_count <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20092,
                            'Duplicate payroll adjustment source links must be reviewed before adding UX_HR_ADJ_SOURCE.');
                    END IF;

                    EXECUTE IMMEDIATE
                        'CREATE UNIQUE INDEX ' || DBMS_ASSERT.ENQUOTE_NAME(current_schema_name, FALSE) ||
                        '."UX_HR_ADJ_SOURCE" ON ' || DBMS_ASSERT.ENQUOTE_NAME(current_schema_name, FALSE) ||
                        '."HR_PAYROLL_ADJUSTMENT" ("COMPANY_ID","SOURCE_TYPE","SOURCE_ID")';
                ELSE
                    SELECT COUNT(*) INTO unique_index_count
                    FROM ALL_INDEXES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND INDEX_NAME = 'UX_HR_ADJ_SOURCE'
                      AND UNIQUENESS = 'UNIQUE';

                    SELECT COUNT(*) INTO valid_index_count
                    FROM ALL_INDEXES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_ADJUSTMENT'
                      AND INDEX_NAME = 'UX_HR_ADJ_SOURCE'
                      AND STATUS = 'VALID';

                    SELECT LISTAGG(COLUMN_NAME, ',') WITHIN GROUP (ORDER BY COLUMN_POSITION)
                    INTO index_columns
                    FROM ALL_IND_COLUMNS
                    WHERE INDEX_OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND INDEX_NAME = 'UX_HR_ADJ_SOURCE';

                    IF unique_index_count <> 1 OR valid_index_count <> 1 OR
                       index_columns <> 'COMPANY_ID,SOURCE_TYPE,SOURCE_ID' THEN
                        RAISE_APPLICATION_ERROR(-20093,
                            'UX_HR_ADJ_SOURCE has an incompatible definition.');
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These nullable columns and idempotency index may predate the migration.
        // Keep the source audit trail and never drop potentially populated data.
    }
}
