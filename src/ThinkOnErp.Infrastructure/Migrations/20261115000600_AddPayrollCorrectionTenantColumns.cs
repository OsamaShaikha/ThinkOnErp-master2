using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000600_AddPayrollCorrectionTenantColumns")]
public sealed class AddPayrollCorrectionTenantColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                invalid_rows NUMBER;
                constraint_count NUMBER;
                constraint_table VARCHAR2(128);
                constraint_type VARCHAR2(1);
                constraint_status VARCHAR2(8);
                constraint_validated VARCHAR2(13);
                data_type VARCHAR2(128);
                char_length NUMBER;
                data_precision NUMBER;
                data_scale NUMBER;
                nullable_flag VARCHAR2(1);
                default_length NUMBER;
                default_expression VARCHAR2(4000);
                virtual_column VARCHAR2(3);

                PROCEDURE inspect_column(
                    p_table VARCHAR2,
                    p_column VARCHAR2,
                    p_kind VARCHAR2,
                    p_length NUMBER DEFAULT NULL,
                    p_precision NUMBER DEFAULT NULL,
                    p_scale NUMBER DEFAULT NULL
                ) IS
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

                    SELECT DATA_TYPE, CHAR_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE,
                           NVL(DEFAULT_LENGTH, 0), DATA_DEFAULT, VIRTUAL_COLUMN
                    INTO data_type, char_length, data_precision, data_scale, nullable_flag,
                         default_length, default_expression, virtual_column
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table AND COLUMN_NAME = p_column;

                    IF p_kind = 'TEXT' THEN
                        IF data_type <> 'NVARCHAR2' OR char_length < p_length OR nullable_flag <> 'Y'
                           OR default_length <> 0 OR virtual_column <> 'NO' THEN
                            RAISE_APPLICATION_ERROR(-20211, p_table || '.' || p_column || ' has an incompatible definition.');
                        END IF;
                    ELSIF p_kind = 'NUMBER_NULL' THEN
                        IF data_type <> 'NUMBER' OR data_precision <> p_precision OR NVL(data_scale, 0) <> p_scale
                           OR nullable_flag <> 'Y' OR default_length <> 0 OR virtual_column <> 'NO' THEN
                            RAISE_APPLICATION_ERROR(-20212, p_table || '.' || p_column || ' has an incompatible definition.');
                        END IF;
                    ELSIF p_kind = 'VERSION' THEN
                        IF data_type <> 'NUMBER' OR data_precision <> 10 OR NVL(data_scale, 0) <> 0
                           OR nullable_flag <> 'N' OR default_length = 0 OR virtual_column <> 'NO'
                           OR default_expression IS NULL
                           OR REGEXP_REPLACE(UPPER(default_expression), '[^A-Z0-9]', '') <> '1' THEN
                            RAISE_APPLICATION_ERROR(-20213, p_table || '.' || p_column || ' must be NUMBER(10) NOT NULL DEFAULT 1.');
                        END IF;
                    ELSIF p_kind = 'TIMESTAMP' THEN
                        IF data_type <> 'TIMESTAMP(7)' OR nullable_flag <> 'Y'
                           OR default_length <> 0 OR virtual_column <> 'NO' THEN
                            RAISE_APPLICATION_ERROR(-20214, p_table || '.' || p_column || ' has an incompatible definition.');
                        END IF;
                    END IF;
                END;

                PROCEDURE preflight_fk(p_name VARCHAR2, p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO constraint_count
                    FROM ALL_CONSTRAINTS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND CONSTRAINT_NAME = p_name;
                    IF constraint_count > 1 THEN
                        RAISE_APPLICATION_ERROR(-20215, p_name || ' is ambiguous in the tenant schema.');
                    ELSIF constraint_count = 1 THEN
                        SELECT TABLE_NAME, CONSTRAINT_TYPE, STATUS, VALIDATED
                        INTO constraint_table, constraint_type, constraint_status, constraint_validated
                        FROM ALL_CONSTRAINTS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND CONSTRAINT_NAME = p_name;

                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_CONSTRAINTS c
                        JOIN ALL_CONSTRAINTS p
                          ON p.OWNER = c.R_OWNER AND p.CONSTRAINT_NAME = c.R_CONSTRAINT_NAME
                        WHERE c.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND c.CONSTRAINT_NAME = p_name
                          AND c.TABLE_NAME = 'HR_PAYROLL_RUN'
                          AND c.CONSTRAINT_TYPE = 'R'
                          AND c.DELETE_RULE = 'NO ACTION'
                          AND p.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND p.TABLE_NAME = 'HR_PAYROLL_RUN'
                          AND p.CONSTRAINT_TYPE IN ('P', 'U')
                          AND p.STATUS = 'ENABLED' AND p.VALIDATED = 'VALIDATED'
                          AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME) = 1
                          AND (SELECT MIN(cc.COLUMN_NAME) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME) = p_column
                          AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME) = 1
                          AND (SELECT MIN(pc.COLUMN_NAME) FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME) = 'ID';

                        IF constraint_table <> 'HR_PAYROLL_RUN' OR constraint_type <> 'R' OR compatible_count <> 1
                           OR constraint_status NOT IN ('ENABLED', 'DISABLED')
                           OR constraint_validated NOT IN ('VALIDATED', 'NOT VALIDATED') THEN
                            RAISE_APPLICATION_ERROR(-20216, p_name || ' does not match the expected self-reference.');
                        END IF;
                    END IF;
                END;

                PROCEDURE preflight_orphans(p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_RUN' AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN RETURN; END IF;

                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_PAYROLL_RUN" child LEFT JOIN "HR_PAYROLL_RUN" parent ON parent."ID"=child."' || p_column || '" WHERE child."' || p_column || '" IS NOT NULL AND parent."ID" IS NULL'
                        INTO invalid_rows;
                    IF invalid_rows <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20217, 'HR_PAYROLL_RUN.' || p_column || ' contains orphan run references.');
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

                PROCEDURE ensure_fk(p_name VARCHAR2, p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO constraint_count
                    FROM ALL_CONSTRAINTS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND CONSTRAINT_NAME = p_name;
                    IF constraint_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_PAYROLL_RUN" ADD CONSTRAINT "' || p_name || '" FOREIGN KEY ("' || p_column || '") REFERENCES "HR_PAYROLL_RUN" ("ID") ENABLE VALIDATE';
                    ELSE
                        SELECT STATUS, VALIDATED INTO constraint_status, constraint_validated
                        FROM ALL_CONSTRAINTS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND CONSTRAINT_NAME = p_name;
                        IF constraint_status <> 'ENABLED' OR constraint_validated <> 'VALIDATED' THEN
                            EXECUTE IMMEDIATE 'ALTER TABLE "HR_PAYROLL_RUN" ENABLE VALIDATE CONSTRAINT "' || p_name || '"';
                        END IF;
                    END IF;
                END;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_PAYROLL_RUN';

                IF table_count = 1 THEN
                    inspect_column('HR_PAYROLL_RUN', 'CORRECTION_OF_RUN_ID', 'NUMBER_NULL', p_precision => 19, p_scale => 0);
                    inspect_column('HR_PAYROLL_RUN', 'CORRECTION_ROOT_RUN_ID', 'NUMBER_NULL', p_precision => 19, p_scale => 0);
                    inspect_column('HR_PAYROLL_RUN', 'CORRECTION_VERSION', 'VERSION');
                    inspect_column('HR_PAYROLL_RUN', 'CORRECTION_REASON', 'TEXT', p_length => 1000);
                    inspect_column('HR_PAYROLL_RUN', 'CANCELLED_BY', 'TEXT', p_length => 200);
                    inspect_column('HR_PAYROLL_RUN', 'CANCELLATION_DATE', 'TIMESTAMP');
                    inspect_column('HR_PAYROLL_RUN', 'CANCELLATION_REASON', 'TEXT', p_length => 1000);

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_CONSTRAINTS p
                    WHERE p.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND p.TABLE_NAME = 'HR_PAYROLL_RUN'
                      AND p.CONSTRAINT_TYPE IN ('P', 'U')
                      AND p.STATUS = 'ENABLED' AND p.VALIDATED = 'VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=p.OWNER AND cc.CONSTRAINT_NAME=p.CONSTRAINT_NAME) = 1
                      AND (SELECT MIN(cc.COLUMN_NAME) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=p.OWNER AND cc.CONSTRAINT_NAME=p.CONSTRAINT_NAME) = 'ID';
                    IF compatible_count = 0 THEN
                        RAISE_APPLICATION_ERROR(-20218, 'HR_PAYROLL_RUN.ID needs an enabled validated primary/unique key before correction FKs can be installed.');
                    END IF;

                    preflight_orphans('CORRECTION_OF_RUN_ID');
                    preflight_orphans('CORRECTION_ROOT_RUN_ID');
                    preflight_fk('FK_HR_PAYRUN_CORR_OF', 'CORRECTION_OF_RUN_ID');
                    preflight_fk('FK_HR_PAYRUN_CORR_ROOT', 'CORRECTION_ROOT_RUN_ID');
                END IF;

                inspect_column('HR_PAYROLL_RUN_LINE', 'BENEFICIARY_NATIONAL_ID', 'TEXT', p_length => 100);

                add_if_missing('HR_PAYROLL_RUN', 'CORRECTION_OF_RUN_ID', 'NUMBER(19) NULL');
                add_if_missing('HR_PAYROLL_RUN', 'CORRECTION_ROOT_RUN_ID', 'NUMBER(19) NULL');
                add_if_missing('HR_PAYROLL_RUN', 'CORRECTION_VERSION', 'NUMBER(10) DEFAULT 1 NOT NULL');
                add_if_missing('HR_PAYROLL_RUN', 'CORRECTION_REASON', 'NVARCHAR2(1000) NULL');
                add_if_missing('HR_PAYROLL_RUN', 'CANCELLED_BY', 'NVARCHAR2(200) NULL');
                add_if_missing('HR_PAYROLL_RUN', 'CANCELLATION_DATE', 'TIMESTAMP(7) NULL');
                add_if_missing('HR_PAYROLL_RUN', 'CANCELLATION_REASON', 'NVARCHAR2(1000) NULL');
                add_if_missing('HR_PAYROLL_RUN_LINE', 'BENEFICIARY_NATIONAL_ID', 'NVARCHAR2(100) NULL');

                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_PAYROLL_RUN';
                IF table_count = 1 THEN
                    ensure_fk('FK_HR_PAYRUN_CORR_OF', 'CORRECTION_OF_RUN_ID');
                    ensure_fk('FK_HR_PAYRUN_CORR_ROOT', 'CORRECTION_ROOT_RUN_ID');
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve payroll correction lineage and cancellation/audit metadata during rollback.
    }
}
