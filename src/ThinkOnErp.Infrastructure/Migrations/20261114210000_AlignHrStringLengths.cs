using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114210000_AlignHrStringLengths")]
public sealed class AlignHrStringLengths : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                column_count NUMBER;
                current_type VARCHAR2(30);
                current_length NUMBER;
                virtual_column VARCHAR2(3);
                too_long_count NUMBER;
            BEGIN
                FOR item IN (
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'APPROVED_BY' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'REASON' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'REJECTION_REASON' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'STATUS' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'EMPLOYEE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'LEAVE_TYPE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'NOTES' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'SHIFT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'STATUS' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'MISSING_PUNCH_HANDLING' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'EMPLOYEE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'SHIFT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'DAY_TYPE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'HOURLY_DIVISOR_FORMULA' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'PAYROLL_TYPE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'PERIOD_CODE' column_name, 40 previous_length, 20 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'STATUS' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'METHOD' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'DESCRIPTION' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'DEVICE_ID' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'EMPLOYEE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'EXTERNAL_REFERENCE' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'PUNCH_TYPE' column_name, 40 previous_length, 20 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'SOURCE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'CALCULATION_TYPE' column_name, 60 previous_length, 30 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'COMPONENT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'COMPONENT_TYPE' column_name, 60 previous_length, 30 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'GL_ACCOUNT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'NAME_LOCAL' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'NAME_LOCAL' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'SHIFT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'WORKING_DAYS_JSON' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL
                ) LOOP
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = item.table_name AND COLUMN_NAME = item.column_name;
                    IF column_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20101, item.table_name || '.' || item.column_name || ' is missing.');
                    END IF;
                    SELECT DATA_TYPE, CHAR_LENGTH, VIRTUAL_COLUMN INTO current_type, current_length, virtual_column FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = item.table_name AND COLUMN_NAME = item.column_name;
                    IF current_type <> 'NVARCHAR2' THEN
                        RAISE_APPLICATION_ERROR(-20102, item.table_name || '.' || item.column_name || ' is not NVARCHAR2.');
                    END IF;
                    IF virtual_column <> 'NO' THEN
                        RAISE_APPLICATION_ERROR(-20106, item.table_name || '.' || item.column_name || ' is virtual and cannot be length-aligned by this migration.');
                    END IF;
                    IF current_length > item.target_length THEN
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM ' || item.table_name || ' WHERE ' || item.column_name ||
                            ' IS NOT NULL AND LENGTH(' || item.column_name || ') > ' || item.target_length INTO too_long_count;
                        IF too_long_count > 0 THEN
                            RAISE_APPLICATION_ERROR(-20103, item.table_name || '.' || item.column_name || ' contains values longer than its current EF limit; review data before migration.');
                        END IF;
                    END IF;
                END LOOP;

                -- Complete all metadata and data checks before the first DDL; Oracle commits DDL implicitly.
                FOR item IN (
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'APPROVED_BY' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'REASON' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'REJECTION_REASON' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'STATUS' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'EMPLOYEE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'LEAVE_TYPE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'NOTES' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'SHIFT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'STATUS' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'MISSING_PUNCH_HANDLING' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'EMPLOYEE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'SHIFT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'DAY_TYPE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'HOURLY_DIVISOR_FORMULA' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'PAYROLL_TYPE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'PERIOD_CODE' column_name, 40 previous_length, 20 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'STATUS' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'METHOD' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'DESCRIPTION' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'DEVICE_ID' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'EMPLOYEE_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'EXTERNAL_REFERENCE' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'PUNCH_TYPE' column_name, 40 previous_length, 20 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'SOURCE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'CALCULATION_TYPE' column_name, 60 previous_length, 30 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'COMPONENT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'COMPONENT_TYPE' column_name, 60 previous_length, 30 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'GL_ACCOUNT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'NAME_LOCAL' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'NAME_LOCAL' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'SHIFT_CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'WORKING_DAYS_JSON' column_name, 1000 previous_length, 500 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'CODE' column_name, 100 previous_length, 50 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'CREATION_USER' column_name, 200 previous_length, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'NAME_AR' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'NAME_EN' column_name, 400 previous_length, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'UPDATE_USER' column_name, 200 previous_length, 100 target_length FROM DUAL
                ) LOOP
                    SELECT CHAR_LENGTH INTO current_length FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = item.table_name AND COLUMN_NAME = item.column_name;
                    IF current_length <> item.target_length THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE ' || item.table_name || ' MODIFY (' || item.column_name || ' NVARCHAR2(' || item.target_length || '))';
                    END IF;
                END LOOP;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Widen to former snapshot lengths. Columns already wider than these values remain untouched.
        migrationBuilder.Sql("""
            DECLARE
                column_count NUMBER;
                current_type VARCHAR2(30);
                current_length NUMBER;
            BEGIN
                FOR item IN (
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'APPROVED_BY' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'REASON' column_name, 1000 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'REJECTION_REASON' column_name, 1000 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'STATUS' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_CORRECTION' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'EMPLOYEE_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'LEAVE_TYPE_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'NOTES' column_name, 1000 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'SHIFT_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'STATUS' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_DAY' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'MISSING_PUNCH_HANDLING' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'NAME_AR' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_ATTENDANCE_POLICY' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'NAME_AR' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_DEDUCTION_POLICY' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'EMPLOYEE_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'SHIFT_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_EMPLOYEE_SHIFT_ASSIGNMENT' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'DAY_TYPE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'HOURLY_DIVISOR_FORMULA' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'NAME_AR' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_OVERTIME_RULE' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'NAME_AR' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'PAYROLL_TYPE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'PERIOD_CODE' column_name, 40 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'STATUS' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PAYROLL_PERIOD' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'METHOD' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'NAME_AR' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PRORATION_POLICY' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'DESCRIPTION' column_name, 1000 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'NAME_AR' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_PUBLIC_HOLIDAY' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'DEVICE_ID' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'EMPLOYEE_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'EXTERNAL_REFERENCE' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'PUNCH_TYPE' column_name, 40 target_length FROM DUAL UNION ALL
                    SELECT 'HR_RAW_ATTENDANCE' table_name, 'SOURCE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'CALCULATION_TYPE' column_name, 60 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'COMPONENT_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'COMPONENT_TYPE' column_name, 60 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'GL_ACCOUNT_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'NAME_LOCAL' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SALARY_COMPONENT' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'NAME_LOCAL' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'SHIFT_CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SHIFT_SCHEDULE' table_name, 'WORKING_DAYS_JSON' column_name, 1000 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'CODE' column_name, 100 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'CREATION_USER' column_name, 200 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'NAME_AR' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'NAME_EN' column_name, 400 target_length FROM DUAL UNION ALL
                    SELECT 'HR_SSC_POLICY' table_name, 'UPDATE_USER' column_name, 200 target_length FROM DUAL
                ) LOOP
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = item.table_name AND COLUMN_NAME = item.column_name;
                    IF column_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20104, item.table_name || '.' || item.column_name || ' is missing.');
                    END IF;
                    SELECT DATA_TYPE, CHAR_LENGTH INTO current_type, current_length FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = item.table_name AND COLUMN_NAME = item.column_name;
                    IF current_type <> 'NVARCHAR2' THEN
                        RAISE_APPLICATION_ERROR(-20105, item.table_name || '.' || item.column_name || ' is not NVARCHAR2.');
                    END IF;
                    IF current_length < item.target_length THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE ' || item.table_name || ' MODIFY (' || item.column_name || ' NVARCHAR2(' || item.target_length || '))';
                    END IF;
                END LOOP;
            END;
            """);
    }
}
