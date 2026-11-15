using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000400_AddShiftAssignmentKind")]
public sealed class AddShiftAssignmentKind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                constraint_count NUMBER;
                constraint_table VARCHAR2(128);
                constraint_type VARCHAR2(1);
                constraint_status VARCHAR2(8);
                constraint_validated VARCHAR2(13);
                constraint_condition VARCHAR2(4000);
                invalid_rows NUMBER;
                expected_condition CONSTANT VARCHAR2(1000) := '(ASSIGNMENT_KIND IS NULL OR ASSIGNMENT_KIND IN (''BASE'',''TEMPORARY'')) AND (ASSIGNMENT_KIND IS NULL OR ASSIGNMENT_KIND<>''TEMPORARY'' OR EFFECTIVE_TO IS NOT NULL)';
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE_SHIFT_ASSIGNMENT';

                -- The EF bootstrap path creates the entire table when it is absent.
                IF table_count = 0 THEN
                    RETURN;
                END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE_SHIFT_ASSIGNMENT'
                  AND COLUMN_NAME = 'ASSIGNMENT_KIND';

                SELECT COUNT(*) INTO compatible_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE_SHIFT_ASSIGNMENT'
                  AND COLUMN_NAME = 'EFFECTIVE_TO';
                IF compatible_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20196, 'HR_EMPLOYEE_SHIFT_ASSIGNMENT.EFFECTIVE_TO is required by CK_HR_SHIFT_ASSIGN_KIND.');
                END IF;

                IF column_count = 1 THEN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_EMPLOYEE_SHIFT_ASSIGNMENT'
                      AND COLUMN_NAME = 'ASSIGNMENT_KIND'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = 20
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20191, 'HR_EMPLOYEE_SHIFT_ASSIGNMENT.ASSIGNMENT_KIND has an incompatible definition.');
                    END IF;

                    SELECT COUNT(*) INTO invalid_rows
                    FROM HR_EMPLOYEE_SHIFT_ASSIGNMENT
                    WHERE ASSIGNMENT_KIND IS NOT NULL
                      AND (ASSIGNMENT_KIND NOT IN ('BASE', 'TEMPORARY')
                           OR (ASSIGNMENT_KIND = 'TEMPORARY' AND EFFECTIVE_TO IS NULL));
                    IF invalid_rows <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20192, 'HR_EMPLOYEE_SHIFT_ASSIGNMENT contains ASSIGNMENT_KIND rows that violate CK_HR_SHIFT_ASSIGN_KIND.');
                    END IF;
                ELSIF column_count <> 0 THEN
                    RAISE_APPLICATION_ERROR(-20193, 'HR_EMPLOYEE_SHIFT_ASSIGNMENT.ASSIGNMENT_KIND metadata is ambiguous.');
                END IF;

                SELECT COUNT(*) INTO constraint_count
                FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND CONSTRAINT_NAME = 'CK_HR_SHIFT_ASSIGN_KIND';

                IF constraint_count > 1 THEN
                    RAISE_APPLICATION_ERROR(-20194, 'CK_HR_SHIFT_ASSIGN_KIND is ambiguous in the tenant schema.');
                ELSIF constraint_count = 1 THEN
                    SELECT TABLE_NAME, CONSTRAINT_TYPE, STATUS, VALIDATED, SEARCH_CONDITION_VC
                    INTO constraint_table, constraint_type, constraint_status, constraint_validated, constraint_condition
                    FROM ALL_CONSTRAINTS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND CONSTRAINT_NAME = 'CK_HR_SHIFT_ASSIGN_KIND';

                    IF constraint_table <> 'HR_EMPLOYEE_SHIFT_ASSIGNMENT'
                       OR constraint_type <> 'C'
                       OR constraint_condition IS NULL
                       OR REPLACE(REGEXP_REPLACE(UPPER(constraint_condition), '[[:space:]]', ''), '"', '')
                          <> REPLACE(REGEXP_REPLACE(UPPER(expected_condition), '[[:space:]]', ''), '"', '')
                       OR constraint_status NOT IN ('ENABLED', 'DISABLED')
                       OR constraint_validated NOT IN ('VALIDATED', 'NOT VALIDATED') THEN
                        RAISE_APPLICATION_ERROR(-20195, 'CK_HR_SHIFT_ASSIGN_KIND is attached to a different object or has an incompatible definition.');
                    END IF;
                END IF;

                -- All compatibility and row checks precede the first tenant DDL.
                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_EMPLOYEE_SHIFT_ASSIGNMENT" ADD ("ASSIGNMENT_KIND" NVARCHAR2(20) NULL)';
                END IF;

                IF constraint_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_EMPLOYEE_SHIFT_ASSIGNMENT" ADD CONSTRAINT "CK_HR_SHIFT_ASSIGN_KIND" CHECK (("ASSIGNMENT_KIND" IS NULL OR "ASSIGNMENT_KIND" IN (''BASE'',''TEMPORARY'')) AND ("ASSIGNMENT_KIND" IS NULL OR "ASSIGNMENT_KIND"<>''TEMPORARY'' OR "EFFECTIVE_TO" IS NOT NULL)) ENABLE VALIDATE';
                ELSIF constraint_status <> 'ENABLED' OR constraint_validated <> 'VALIDATED' THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_EMPLOYEE_SHIFT_ASSIGNMENT" ENABLE VALIDATE CONSTRAINT "CK_HR_SHIFT_ASSIGN_KIND"';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve assignment classification; NULL continues to represent legacy date-based assignments.
    }
}
