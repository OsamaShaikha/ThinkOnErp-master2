using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114120000_AddDepartmentManagerEmployeeCode")]
public sealed class AddDepartmentManagerEmployeeCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The schema tool may already have applied this nullable extension. Keep
        // the EF migration safe to record after that patch and reject incompatible
        // existing definitions before any schema change.
        migrationBuilder.Sql("""
            DECLARE
                department_count NUMBER;
                employee_count NUMBER;
                manager_count NUMBER;
                compatible_count NUMBER;
                employee_type VARCHAR2(30);
                employee_length NUMBER;
                employee_data_length NUMBER;
                employee_char_used VARCHAR2(1);
                column_definition VARCHAR2(100);
            BEGIN
                SELECT COUNT(*) INTO department_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_DEPARTMENT';
                SELECT COUNT(*) INTO employee_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE';

                IF department_count <> 1 OR employee_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20071, 'HR_DEPARTMENT and HR_EMPLOYEE must exist in the current schema.');
                END IF;

                SELECT DATA_TYPE, CHAR_LENGTH, DATA_LENGTH, CHAR_USED
                INTO employee_type, employee_length, employee_data_length, employee_char_used
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE'
                  AND COLUMN_NAME = 'EMPLOYEE_CODE';

                IF employee_type NOT IN ('NVARCHAR2', 'VARCHAR2', 'NCHAR', 'CHAR')
                   OR employee_length <= 0 OR employee_length > 2000 THEN
                    RAISE_APPLICATION_ERROR(-20072, 'HR_EMPLOYEE.EMPLOYEE_CODE has an unsupported character definition.');
                END IF;

                column_definition := employee_type || '(' || employee_length;
                IF employee_type IN ('VARCHAR2', 'CHAR') AND employee_char_used = 'B' THEN
                    column_definition := employee_type || '(' || employee_data_length || ' BYTE';
                ELSIF employee_type IN ('VARCHAR2', 'CHAR') THEN
                    column_definition := column_definition || ' CHAR';
                END IF;
                column_definition := column_definition || ')';

                SELECT COUNT(*) INTO manager_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_DEPARTMENT'
                  AND COLUMN_NAME = 'MANAGER_EMPLOYEE_CODE';

                IF manager_count = 0 THEN
                    EXECUTE IMMEDIATE
                        'ALTER TABLE HR_DEPARTMENT ADD (MANAGER_EMPLOYEE_CODE ' || column_definition || ' NULL)';
                ELSE
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_DEPARTMENT'
                      AND COLUMN_NAME = 'MANAGER_EMPLOYEE_CODE'
                      AND DATA_TYPE = employee_type
                      AND CHAR_LENGTH >= employee_length
                      AND NVL(CHAR_USED, '-') = NVL(employee_char_used, '-')
                      AND (employee_char_used <> 'B' OR DATA_LENGTH >= employee_data_length)
                      AND NULLABLE = 'Y'
                      AND DATA_DEFAULT IS NULL
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20073, 'HR_DEPARTMENT.MANAGER_EMPLOYEE_CODE has an incompatible definition.');
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A previous schema-tool patch may own this column and contain historical
        // references. Roll forward rather than risk deleting department data.
    }
}
