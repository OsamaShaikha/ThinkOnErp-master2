using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114200000_AlignEmployeeDependentTextLengths")]
public sealed class AlignEmployeeDependentTextLengths : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                current_type VARCHAR2(30);
                current_length NUMBER;
                current_nullable VARCHAR2(1);
                virtual_column VARCHAR2(3);
                too_long_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE_DEPENDENT';

                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20091, 'HR_EMPLOYEE_DEPENDENT is missing in the current schema.');
                END IF;

                FOR item IN (
                    SELECT 'EMPLOYEE_CODE' column_name, 50 target_length, 'N' nullable_flag FROM DUAL UNION ALL
                    SELECT 'NAME_LOCAL', 200, 'N' FROM DUAL UNION ALL
                    SELECT 'NAME_EN', 200, 'N' FROM DUAL UNION ALL
                    SELECT 'RELATIONSHIP', 30, 'N' FROM DUAL UNION ALL
                    SELECT 'GENDER', 10, 'N' FROM DUAL UNION ALL
                    SELECT 'NATIONAL_ID', 50, 'Y' FROM DUAL UNION ALL
                    SELECT 'CREATION_USER', 100, 'N' FROM DUAL UNION ALL
                    SELECT 'UPDATE_USER', 100, 'Y' FROM DUAL
                ) LOOP
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_EMPLOYEE_DEPENDENT'
                      AND COLUMN_NAME = item.column_name;

                    IF column_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20092, 'HR_EMPLOYEE_DEPENDENT.' || item.column_name || ' is missing.');
                    END IF;

                    SELECT DATA_TYPE, CHAR_LENGTH, NULLABLE, VIRTUAL_COLUMN
                    INTO current_type, current_length, current_nullable, virtual_column
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_EMPLOYEE_DEPENDENT'
                      AND COLUMN_NAME = item.column_name;

                    IF current_type <> 'NVARCHAR2' OR current_nullable <> item.nullable_flag THEN
                        RAISE_APPLICATION_ERROR(-20093, 'HR_EMPLOYEE_DEPENDENT.' || item.column_name || ' has an incompatible type or nullability.');
                    END IF;
                    IF virtual_column <> 'NO' THEN
                        RAISE_APPLICATION_ERROR(-20097, 'HR_EMPLOYEE_DEPENDENT.' || item.column_name || ' is virtual and cannot be length-aligned by this migration.');
                    END IF;

                    IF current_length > item.target_length THEN
                        EXECUTE IMMEDIATE
                            'SELECT COUNT(*) FROM HR_EMPLOYEE_DEPENDENT WHERE ' || item.column_name ||
                            ' IS NOT NULL AND LENGTH(' || item.column_name || ') > ' || item.target_length
                            INTO too_long_count;
                        IF too_long_count > 0 THEN
                            RAISE_APPLICATION_ERROR(-20094, 'HR_EMPLOYEE_DEPENDENT.' || item.column_name || ' contains values longer than the current EF limit; review data before migration.');
                        END IF;
                    END IF;

                END LOOP;

                -- Complete all metadata and data checks before the first Oracle DDL,
                -- since ALTER TABLE commits implicitly and cannot be rolled back.
                FOR item IN (
                    SELECT 'EMPLOYEE_CODE' column_name, 50 target_length FROM DUAL UNION ALL
                    SELECT 'NAME_LOCAL', 200 FROM DUAL UNION ALL
                    SELECT 'NAME_EN', 200 FROM DUAL UNION ALL
                    SELECT 'RELATIONSHIP', 30 FROM DUAL UNION ALL
                    SELECT 'GENDER', 10 FROM DUAL UNION ALL
                    SELECT 'NATIONAL_ID', 50 FROM DUAL UNION ALL
                    SELECT 'CREATION_USER', 100 FROM DUAL UNION ALL
                    SELECT 'UPDATE_USER', 100 FROM DUAL
                ) LOOP
                    SELECT CHAR_LENGTH INTO current_length
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_EMPLOYEE_DEPENDENT'
                      AND COLUMN_NAME = item.column_name;

                    IF current_length <> item.target_length THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE_DEPENDENT MODIFY (' || item.column_name || ' NVARCHAR2(' || item.target_length || '))';
                    END IF;
                END LOOP;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restore the original wider schema without truncating data. The original
        // widths came from the table-creation migration and remain safe to widen to.
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                current_type VARCHAR2(30);
                current_length NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE_DEPENDENT';
                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20095, 'HR_EMPLOYEE_DEPENDENT is missing in the current schema.');
                END IF;

                FOR item IN (
                    SELECT 'EMPLOYEE_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'NAME_LOCAL', 400 FROM DUAL UNION ALL
                    SELECT 'NAME_EN', 400 FROM DUAL UNION ALL
                    SELECT 'RELATIONSHIP', 60 FROM DUAL UNION ALL
                    SELECT 'GENDER', 20 FROM DUAL UNION ALL
                    SELECT 'NATIONAL_ID', 100 FROM DUAL UNION ALL
                    SELECT 'CREATION_USER', 200 FROM DUAL UNION ALL
                    SELECT 'UPDATE_USER', 200 FROM DUAL
                ) LOOP
                    SELECT DATA_TYPE, CHAR_LENGTH
                    INTO current_type, current_length
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_EMPLOYEE_DEPENDENT'
                      AND COLUMN_NAME = item.column_name;

                    IF current_type <> 'NVARCHAR2' THEN
                        RAISE_APPLICATION_ERROR(-20096, 'HR_EMPLOYEE_DEPENDENT.' || item.column_name || ' has an incompatible type.');
                    END IF;
                    IF current_length < item.target_length THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE_DEPENDENT MODIFY (' || item.column_name || ' NVARCHAR2(' || item.target_length || '))';
                    END IF;
                END LOOP;
            END;
            """);
    }
}
