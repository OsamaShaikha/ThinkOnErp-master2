using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "WEIGHT",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "STANDARD_COST",
                table: "INV_ITEM",
                type: "NUMBER(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(18,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<bool>(
                name: "SERIAL_TRACKING",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "SAFETY_STOCK",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "REORDER_POINT",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "MIN_ORDER_QTY",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<bool>(
                name: "LOT_TRACKING",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "LEAD_TIME_DAYS",
                table: "INV_ITEM",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "EXPIRY_TRACKING",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "ALLOW_NEGATIVE_STOCK",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)",
                oldDefaultValue: false);

            migrationBuilder.CreateTable(
                name: "HR_ATTENDANCE_CORRECTION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ATTENDANCE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    OLD_CHECK_IN = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    OLD_CHECK_OUT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REQUESTED_CHECK_IN = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REQUESTED_CHECK_OUT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REASON = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    APPROVED_BY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    APPROVAL_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REJECTION_REASON = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_ATTENDANCE_CORRECTION", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_ATTENDANCE_DAY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ATTENDANCE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    WORK_CALENDAR_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SHIFT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SCHEDULED_HOURS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    FIRST_CHECK_IN = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LAST_CHECK_OUT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ACTUAL_WORKED_HOURS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    LATE_ARRIVAL_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EARLY_LEAVE_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    OVERTIME_HOURS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    HAS_MISSING_PUNCH = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    LEAVE_TYPE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_ATTENDANCE_DAY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_ATTENDANCE_POLICY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    GRACE_PERIOD_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EARLY_LEAVE_TOLERANCE_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MIN_MINUTES_FOR_OVERTIME = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    AUTO_DEDUCT_LATE_ARRIVAL = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    MISSING_PUNCH_HANDLING = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_ATTENDANCE_POLICY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_DEDUCTION_POLICY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MAX_DEDUCTION_PERCENTAGE = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    MIN_NET_PAY_GUARANTEE = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    AUTO_CAP_AND_CARRY_FORWARD = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ALLOW_NEGATIVE_NET_PAY = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_DEDUCTION_POLICY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_EMPLOYEE",
                columns: table => new
                {
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NATIONAL_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NATIONALITY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PASSPORT_NUMBER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    DATE_OF_BIRTH = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    GENDER = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    MARITAL_STATUS = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    PHONE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    HIRE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    PROBATION_END_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TERMINATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TERMINATION_REASON = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    EMPLOYMENT_TYPE = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    EMPLOYMENT_STATUS = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    DEPARTMENT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    POSITION_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    MANAGER_EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SSC_NUMBER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    TAX_EXEMPTION_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_HIGH_RISK_ROLE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    BANK_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    BANK_ACCOUNT_NUMBER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    BANK_IBAN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EMPLOYEE", x => x.EMPLOYEE_CODE);
                });

            migrationBuilder.CreateTable(
                name: "HR_EMPLOYEE_SHIFT_ASSIGNMENT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SHIFT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EMPLOYEE_SHIFT_ASSIGNMENT", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_OVERTIME_RULE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DAY_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    MULTIPLIER = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    MINIMUM_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MAXIMUM_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HOURLY_DIVISOR_FORMULA = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    REQUIRES_APPROVAL = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_OVERTIME_RULE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_PAYROLL_PERIOD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PERIOD_CODE = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    FISCAL_YEAR = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MONTH = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    START_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    END_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    PAY_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    PAYROLL_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PAYROLL_PERIOD", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_PAYROLL_RUN",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PAY_PERIOD = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    RUN_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    TOTAL_GROSS_SALARY = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_NET_SALARY = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_EMPLOYEE_SSC = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_EMPLOYER_SSC = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_INCOME_TAX = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_NATIONAL_CONTRIB = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_OTHER_DEDUCTIONS = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    CALCULATED_BY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    CALCULATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    APPROVED_BY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    APPROVAL_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    POSTED_BY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    POST_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    PAID_BY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    PAYMENT_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    JOURNAL_VOUCHER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PAYROLL_RUN", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_PRORATION_POLICY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    METHOD = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PRORATION_POLICY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_PUBLIC_HOLIDAY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    HOLIDAY_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    IS_PAID = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_RECURRING = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PUBLIC_HOLIDAY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_RAW_ATTENDANCE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PUNCH_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    PUNCH_TYPE = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    DEVICE_ID = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    EXTERNAL_REFERENCE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    SOURCE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    IS_PROCESSED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PROCESSED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_RAW_ATTENDANCE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_SALARY_COMPONENT",
                columns: table => new
                {
                    COMPONENT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    COMPONENT_TYPE = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    CALCULATION_TYPE = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    DEFAULT_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: true),
                    DEFAULT_PERCENT = table.Column<decimal>(type: "DECIMAL(8,4)", precision: 8, scale: 4, nullable: true),
                    IS_TAXABLE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_SSC_APPLICABLE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    GL_ACCOUNT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SALARY_COMPONENT", x => x.COMPONENT_CODE);
                });

            migrationBuilder.CreateTable(
                name: "HR_SHIFT_SCHEDULE",
                columns: table => new
                {
                    SHIFT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    START_TIME = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: false),
                    END_TIME = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: false),
                    BREAK_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WORKING_DAYS_JSON = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SHIFT_SCHEDULE", x => x.SHIFT_CODE);
                });

            migrationBuilder.CreateTable(
                name: "HR_SSC_POLICY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    EMPLOYEE_CONTRIB_RATE = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    EMPLOYER_CONTRIB_RATE = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    HIGH_RISK_SURCHARGE_RATE = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    MONTHLY_CEILING_CAP = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    MINIMUM_WAGE_FLOOR = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SSC_POLICY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_TAX_POLICY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    PERSONAL_EXEMPTION_SELF = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    PERSONAL_EXEMPTION_DEPENDENT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    NATIONAL_CONTRIB_THRESHOLD = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    NATIONAL_CONTRIB_RATE = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    IS_SSC_TAX_DEDUCTIBLE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CALCULATION_FREQUENCY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_TAX_POLICY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_WORK_CALENDAR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_WORK_CALENDAR", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HR_EMPLOYEE_ADVANCE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ADVANCE_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TARGET_PAY_PERIOD = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    DEDUCTED_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    REMAINING_BALANCE = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    APPROVED_BY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    APPROVAL_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REASON = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EMPLOYEE_ADVANCE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_EMPLOYEE_ADVANCE_HR_EMPLOYEE_EMPLOYEE_CODE",
                        column: x => x.EMPLOYEE_CODE,
                        principalTable: "HR_EMPLOYEE",
                        principalColumn: "EMPLOYEE_CODE",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HR_EMPLOYEE_DEPENDENT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    RELATIONSHIP = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    DATE_OF_BIRTH = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    GENDER = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    NATIONAL_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    IS_TAX_EXEMPTION_CLAIMED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_MEDICAL_COVERED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EMPLOYEE_DEPENDENT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_EMPLOYEE_DEPENDENT_HR_EMPLOYEE_EMPLOYEE_CODE",
                        column: x => x.EMPLOYEE_CODE,
                        principalTable: "HR_EMPLOYEE",
                        principalColumn: "EMPLOYEE_CODE",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_EMPLOYEE_LOAN",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    LOAN_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PRINCIPAL_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    MONTHLY_INSTALLMENT_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_INSTALLMENTS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TOTAL_PAID_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    REMAINING_BALANCE = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    START_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    END_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    APPROVED_BY = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    APPROVAL_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EMPLOYEE_LOAN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_EMPLOYEE_LOAN_HR_EMPLOYEE_EMPLOYEE_CODE",
                        column: x => x.EMPLOYEE_CODE,
                        principalTable: "HR_EMPLOYEE",
                        principalColumn: "EMPLOYEE_CODE",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HR_SALARY_STRUCTURE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    EFFECTIVE_FROM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EFFECTIVE_TO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    BASIC_SALARY = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    CURRENCY_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    PAYMENT_METHOD = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SALARY_STRUCTURE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_SALARY_STRUCTURE_HR_EMPLOYEE_EMPLOYEE_CODE",
                        column: x => x.EMPLOYEE_CODE,
                        principalTable: "HR_EMPLOYEE",
                        principalColumn: "EMPLOYEE_CODE",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_PAYROLL_RUN_LINE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PAYROLL_RUN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    DEPARTMENT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    COST_CENTER_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    BASIC_SALARY = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_EARNINGS = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    GROSS_SALARY = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    SSC_ELIGIBLE_SALARY = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    SSC_EMPLOYEE_CONTRIB = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    SSC_EMPLOYER_CONTRIB = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TAXABLE_GROSS = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    ANNUAL_EXEMPTIONS = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    ANNUAL_TAXABLE_NET = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    INCOME_TAX_WITHHELD = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    NATIONAL_CONTRIB_WITHHELD = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    OTHER_DEDUCTIONS = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TOTAL_DEDUCTIONS = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    NET_PAY = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    PAYMENT_METHOD = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    BANK_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    IBAN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PAYROLL_RUN_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_PAYROLL_RUN_LINE_HR_EMPLOYEE_EMPLOYEE_CODE",
                        column: x => x.EMPLOYEE_CODE,
                        principalTable: "HR_EMPLOYEE",
                        principalColumn: "EMPLOYEE_CODE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HR_PAYROLL_RUN_LINE_HR_PAYROLL_RUN_PAYROLL_RUN_ID",
                        column: x => x.PAYROLL_RUN_ID,
                        principalTable: "HR_PAYROLL_RUN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_PAYROLL_ADJUSTMENT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PAY_PERIOD = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    COMPONENT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ADJUSTMENT_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    PAYROLL_RUN_LINE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PAYROLL_ADJUSTMENT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_PAYROLL_ADJUSTMENT_HR_EMPLOYEE_EMPLOYEE_CODE",
                        column: x => x.EMPLOYEE_CODE,
                        principalTable: "HR_EMPLOYEE",
                        principalColumn: "EMPLOYEE_CODE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HR_PAYROLL_ADJUSTMENT_HR_SALARY_COMPONENT_COMPONENT_CODE",
                        column: x => x.COMPONENT_CODE,
                        principalTable: "HR_SALARY_COMPONENT",
                        principalColumn: "COMPONENT_CODE",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HR_TAX_BRACKET",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TAX_POLICY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BRACKET_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LOWER_LIMIT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    UPPER_LIMIT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: true),
                    RATE_PERCENT = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_TAX_BRACKET", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_TAX_BRACKET_HR_TAX_POLICY_TAX_POLICY_ID",
                        column: x => x.TAX_POLICY_ID,
                        principalTable: "HR_TAX_POLICY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_WORK_CALENDAR_DAY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    WORK_CALENDAR_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DAY_OF_WEEK = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_WORKING_DAY = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    STANDARD_WORKING_HOURS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    DEFAULT_SHIFT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_WORK_CALENDAR_DAY", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_WORK_CALENDAR_DAY_HR_WORK_CALENDAR_WORK_CALENDAR_ID",
                        column: x => x.WORK_CALENDAR_ID,
                        principalTable: "HR_WORK_CALENDAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_LOAN_REPAYMENT_SCHEDULE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EMPLOYEE_LOAN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    INSTALLMENT_NO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PAY_PERIOD = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    SCHEDULED_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    PAID_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    CARRIED_FORWARD_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PAID_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    PAYROLL_RUN_LINE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_LOAN_REPAYMENT_SCHEDULE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_LOAN_REPAYMENT_SCHEDULE_HR_EMPLOYEE_LOAN_EMPLOYEE_LOAN_ID",
                        column: x => x.EMPLOYEE_LOAN_ID,
                        principalTable: "HR_EMPLOYEE_LOAN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_SALARY_STRUCTURE_LINE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    STRUCTURE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    COMPONENT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    AMOUNT = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    PERCENT = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SALARY_STRUCTURE_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_SALARY_STRUCTURE_LINE_HR_SALARY_COMPONENT_COMPONENT_CODE",
                        column: x => x.COMPONENT_CODE,
                        principalTable: "HR_SALARY_COMPONENT",
                        principalColumn: "COMPONENT_CODE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HR_SALARY_STRUCTURE_LINE_HR_SALARY_STRUCTURE_STRUCTURE_ID",
                        column: x => x.STRUCTURE_ID,
                        principalTable: "HR_SALARY_STRUCTURE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_PAYROLL_CALC_SNAPSHOT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PAYROLL_RUN_LINE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    EMPLOYEE_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PAY_PERIOD = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    CALCULATION_TIMESTAMP = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    TOTAL_BASE_DAYS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    ELIGIBLE_DAYS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    PRORATION_FACTOR = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    PRORATION_POLICY_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PRORATION_METHOD_USED = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    OVERTIME_HOURS_APPLIED = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    OVERTIME_EARNINGS_APPLIED = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    SSC_POLICY_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SSC_EMP_RATE_APPLIED = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    SSC_EMPR_RATE_APPLIED = table.Column<decimal>(type: "DECIMAL(10,6)", precision: 10, scale: 6, nullable: false),
                    SSC_CAP_APPLIED = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    TAX_POLICY_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    TAX_EXEMPTIONS_APPLIED = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    LOAN_DEDUCTIONS_APPLIED = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    LOAN_DEDUCTIONS_CARRIED_FWD = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    EXPLANATION_JSON = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PAYROLL_CALC_SNAPSHOT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_PAYROLL_CALC_SNAPSHOT_HR_PAYROLL_RUN_LINE_PAYROLL_RUN_LINE_ID",
                        column: x => x.PAYROLL_RUN_LINE_ID,
                        principalTable: "HR_PAYROLL_RUN_LINE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_PAYROLL_RUN_LINE_COMPONENT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PAYROLL_LINE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    COMPONENT_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    COMPONENT_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    COMPONENT_NAME_EN = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    COMPONENT_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    AMOUNT = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PAYROLL_RUN_LINE_COMPONENT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HR_PAYROLL_RUN_LINE_COMPONENT_HR_PAYROLL_RUN_LINE_PAYROLL_LINE_ID",
                        column: x => x.PAYROLL_LINE_ID,
                        principalTable: "HR_PAYROLL_RUN_LINE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EMPLOYEE_ADVANCE_EMPLOYEE_CODE",
                table: "HR_EMPLOYEE_ADVANCE",
                column: "EMPLOYEE_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_EMPLOYEE_DEPENDENT_EMPLOYEE_CODE",
                table: "HR_EMPLOYEE_DEPENDENT",
                column: "EMPLOYEE_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_EMPLOYEE_LOAN_EMPLOYEE_CODE",
                table: "HR_EMPLOYEE_LOAN",
                column: "EMPLOYEE_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_LOAN_REPAYMENT_SCHEDULE_EMPLOYEE_LOAN_ID",
                table: "HR_LOAN_REPAYMENT_SCHEDULE",
                column: "EMPLOYEE_LOAN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PAYROLL_ADJUSTMENT_COMPONENT_CODE",
                table: "HR_PAYROLL_ADJUSTMENT",
                column: "COMPONENT_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PAYROLL_ADJUSTMENT_EMPLOYEE_CODE",
                table: "HR_PAYROLL_ADJUSTMENT",
                column: "EMPLOYEE_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PAYROLL_CALC_SNAPSHOT_PAYROLL_RUN_LINE_ID",
                table: "HR_PAYROLL_CALC_SNAPSHOT",
                column: "PAYROLL_RUN_LINE_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PAYROLL_RUN_LINE_EMPLOYEE_CODE",
                table: "HR_PAYROLL_RUN_LINE",
                column: "EMPLOYEE_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PAYROLL_RUN_LINE_PAYROLL_RUN_ID",
                table: "HR_PAYROLL_RUN_LINE",
                column: "PAYROLL_RUN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PAYROLL_RUN_LINE_COMPONENT_PAYROLL_LINE_ID",
                table: "HR_PAYROLL_RUN_LINE_COMPONENT",
                column: "PAYROLL_LINE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SALARY_STRUCTURE_EMPLOYEE_CODE",
                table: "HR_SALARY_STRUCTURE",
                column: "EMPLOYEE_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SALARY_STRUCTURE_LINE_COMPONENT_CODE",
                table: "HR_SALARY_STRUCTURE_LINE",
                column: "COMPONENT_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SALARY_STRUCTURE_LINE_STRUCTURE_ID",
                table: "HR_SALARY_STRUCTURE_LINE",
                column: "STRUCTURE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HR_TAX_BRACKET_TAX_POLICY_ID",
                table: "HR_TAX_BRACKET",
                column: "TAX_POLICY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HR_WORK_CALENDAR_DAY_WORK_CALENDAR_ID",
                table: "HR_WORK_CALENDAR_DAY",
                column: "WORK_CALENDAR_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_ATTENDANCE_CORRECTION");

            migrationBuilder.DropTable(
                name: "HR_ATTENDANCE_DAY");

            migrationBuilder.DropTable(
                name: "HR_ATTENDANCE_POLICY");

            migrationBuilder.DropTable(
                name: "HR_DEDUCTION_POLICY");

            migrationBuilder.DropTable(
                name: "HR_EMPLOYEE_ADVANCE");

            migrationBuilder.DropTable(
                name: "HR_EMPLOYEE_DEPENDENT");

            migrationBuilder.DropTable(
                name: "HR_EMPLOYEE_SHIFT_ASSIGNMENT");

            migrationBuilder.DropTable(
                name: "HR_LOAN_REPAYMENT_SCHEDULE");

            migrationBuilder.DropTable(
                name: "HR_OVERTIME_RULE");

            migrationBuilder.DropTable(
                name: "HR_PAYROLL_ADJUSTMENT");

            migrationBuilder.DropTable(
                name: "HR_PAYROLL_CALC_SNAPSHOT");

            migrationBuilder.DropTable(
                name: "HR_PAYROLL_PERIOD");

            migrationBuilder.DropTable(
                name: "HR_PAYROLL_RUN_LINE_COMPONENT");

            migrationBuilder.DropTable(
                name: "HR_PRORATION_POLICY");

            migrationBuilder.DropTable(
                name: "HR_PUBLIC_HOLIDAY");

            migrationBuilder.DropTable(
                name: "HR_RAW_ATTENDANCE");

            migrationBuilder.DropTable(
                name: "HR_SALARY_STRUCTURE_LINE");

            migrationBuilder.DropTable(
                name: "HR_SHIFT_SCHEDULE");

            migrationBuilder.DropTable(
                name: "HR_SSC_POLICY");

            migrationBuilder.DropTable(
                name: "HR_TAX_BRACKET");

            migrationBuilder.DropTable(
                name: "HR_WORK_CALENDAR_DAY");

            migrationBuilder.DropTable(
                name: "HR_EMPLOYEE_LOAN");

            migrationBuilder.DropTable(
                name: "HR_PAYROLL_RUN_LINE");

            migrationBuilder.DropTable(
                name: "HR_SALARY_COMPONENT");

            migrationBuilder.DropTable(
                name: "HR_SALARY_STRUCTURE");

            migrationBuilder.DropTable(
                name: "HR_TAX_POLICY");

            migrationBuilder.DropTable(
                name: "HR_WORK_CALENDAR");

            migrationBuilder.DropTable(
                name: "HR_PAYROLL_RUN");

            migrationBuilder.DropTable(
                name: "HR_EMPLOYEE");

            migrationBuilder.AlterColumn<decimal>(
                name: "WEIGHT",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "STANDARD_COST",
                table: "INV_ITEM",
                type: "NUMBER(18,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(18,4)");

            migrationBuilder.AlterColumn<bool>(
                name: "SERIAL_TRACKING",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SAFETY_STOCK",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "REORDER_POINT",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "MIN_ORDER_QTY",
                table: "INV_ITEM",
                type: "NUMBER(14,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "NUMBER(14,4)");

            migrationBuilder.AlterColumn<bool>(
                name: "LOT_TRACKING",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)");

            migrationBuilder.AlterColumn<int>(
                name: "LEAD_TIME_DAYS",
                table: "INV_ITEM",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<bool>(
                name: "EXPIRY_TRACKING",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "ALLOW_NEGATIVE_STOCK",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "NUMBER(1)");
        }
    }
}
