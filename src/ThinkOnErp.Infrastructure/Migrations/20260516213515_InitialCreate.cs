using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_AUDIT_LOG",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ACTOR_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ACTOR_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ACTION = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ENTITY_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ENTITY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    OLD_VALUE = table.Column<string>(type: "CLOB", nullable: true),
                    NEW_VALUE = table.Column<string>(type: "CLOB", nullable: true),
                    IP_ADDRESS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    USER_AGENT = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CORRELATION_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    HTTP_METHOD = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    ENDPOINT_PATH = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    REQUEST_PAYLOAD = table.Column<string>(type: "CLOB", nullable: true),
                    RESPONSE_PAYLOAD = table.Column<string>(type: "CLOB", nullable: true),
                    EXECUTION_TIME_MS = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    STATUS_CODE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    EXCEPTION_TYPE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    EXCEPTION_MESSAGE = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    STACK_TRACE = table.Column<string>(type: "CLOB", nullable: true),
                    SEVERITY = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    EVENT_CATEGORY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    METADATA = table.Column<string>(type: "CLOB", nullable: true),
                    BUSINESS_MODULE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    DEVICE_IDENTIFIER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    ERROR_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    BUSINESS_DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_AUDIT_LOG", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_AUDIT_LOG_ARCHIVE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ACTOR_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ACTOR_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ACTION = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ENTITY_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ENTITY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    OLD_VALUE = table.Column<string>(type: "CLOB", nullable: true),
                    NEW_VALUE = table.Column<string>(type: "CLOB", nullable: true),
                    IP_ADDRESS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    USER_AGENT = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CORRELATION_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    HTTP_METHOD = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    ENDPOINT_PATH = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    REQUEST_PAYLOAD = table.Column<string>(type: "CLOB", nullable: true),
                    RESPONSE_PAYLOAD = table.Column<string>(type: "CLOB", nullable: true),
                    EXECUTION_TIME_MS = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    STATUS_CODE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    EXCEPTION_TYPE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    EXCEPTION_MESSAGE = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    STACK_TRACE = table.Column<string>(type: "CLOB", nullable: true),
                    SEVERITY = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    EVENT_CATEGORY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    METADATA = table.Column<string>(type: "CLOB", nullable: true),
                    BUSINESS_MODULE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    DEVICE_IDENTIFIER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    ERROR_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    BUSINESS_DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ARCHIVED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ARCHIVE_BATCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_AUDIT_LOG_ARCHIVE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_COMPANY_SYSTEMS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SYSTEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    IS_ALLOWED = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REVOKED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_COMPANY_SYSTEMS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_CURRENCY",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SHORT_DESC = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    SHORT_DESC_E = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    SINGULER_DESC = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    SINGULER_DESC_E = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DUAL_DESC = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DUAL_DESC_E = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    SUM_DESC = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    SUM_DESC_E = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    FRAC_DESC = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    FRAC_DESC_E = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CURR_RATE = table.Column<decimal>(type: "DECIMAL(18,6)", nullable: true),
                    CURR_RATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_CURRENCY", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_FAILED_LOGINS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    IP_ADDRESS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    USERNAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    FAILURE_REASON = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    ATTEMPT_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_FAILED_LOGINS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_PERFORMANCE_METRICS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ENDPOINT_PATH = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    HOUR_TIMESTAMP = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    REQUEST_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    AVG_EXECUTION_TIME_MS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    MIN_EXECUTION_TIME_MS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    MAX_EXECUTION_TIME_MS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    P50_EXECUTION_TIME_MS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    P95_EXECUTION_TIME_MS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    P99_EXECUTION_TIME_MS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    AVG_DATABASE_TIME_MS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    AVG_QUERY_COUNT = table.Column<decimal>(type: "NUMBER", nullable: false),
                    ERROR_COUNT = table.Column<decimal>(type: "NUMBER", nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_PERFORMANCE_METRICS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_REPORT_SCHEDULE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    REPORT_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    FREQUENCY = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    DAY_OF_WEEK = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DAY_OF_MONTH = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    TIME_OF_DAY = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    RECIPIENTS = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    EXPORT_FORMAT = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    PARAMETERS = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATED_BY_USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LAST_GENERATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LAST_GENERATION_STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    LAST_ERROR_MESSAGE = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_REPORT_SCHEDULE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_RETENTION_POLICIES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENT_CATEGORY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    RETENTION_DAYS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ARCHIVE_ENABLED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    LAST_MODIFIED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LAST_MODIFIED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_RETENTION_POLICIES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_ROLE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NOTE = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_ROLE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_SECURITY_THREATS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    THREAT_TYPE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SEVERITY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    IP_ADDRESS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    DESCRIPTION = table.Column<string>(type: "CLOB", nullable: false),
                    DETECTION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    METADATA = table.Column<string>(type: "CLOB", nullable: true),
                    ACKNOWLEDGED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ACKNOWLEDGED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    RESOLVED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SECURITY_THREATS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_SLOW_QUERIES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CORRELATION_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SQL_STATEMENT = table.Column<string>(type: "CLOB", nullable: false),
                    EXECUTION_TIME_MS = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ROWS_AFFECTED = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ENDPOINT_PATH = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SLOW_QUERIES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_SUPER_ADMIN",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    USER_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PASSWORD = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    PHONE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TWO_FA_SECRET = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    TWO_FA_ENABLED = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    LAST_LOGIN_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SUPER_ADMIN", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_SYSTEM",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    SYSTEM_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    SYSTEM_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SYSTEM_NAME_E = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DESCRIPTION_E = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    ICON = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    DISPLAY_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SYSTEM", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_TICKET_CATEGORY",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CATEGORY_NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CATEGORY_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DESCRIPTION_AR = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DESCRIPTION_EN = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DISPLAY_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_TICKET_CATEGORY", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_TICKET_CONFIG",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CONFIG_KEY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CONFIG_VALUE = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    CONFIG_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DESCRIPTION_AR = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DESCRIPTION_EN = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_TICKET_CONFIG", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_TICKET_PRIORITY",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PRIORITY_NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PRIORITY_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PRIORITY_LEVEL = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SLA_TARGET_HOURS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    ESCALATION_THRESHOLD_HOURS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_TICKET_PRIORITY", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_TICKET_STATUS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    STATUS_NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    STATUS_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    STATUS_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DISPLAY_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_FINAL_STATUS = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_TICKET_STATUS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_USERS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    USER_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PASSWORD = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    PHONE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    PHONE2 = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    ROLE = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    LAST_LOGIN_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    IS_ADMIN = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REFRESH_TOKEN = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    REFRESH_TOKEN_EXPIRY = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    FORCE_LOGOUT_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_USERS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SYS_SCREEN",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    SYSTEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PARENT_SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SCREEN_CODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SCREEN_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SCREEN_NAME_E = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ROUTE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DESCRIPTION_E = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    ICON = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    DISPLAY_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SCREEN", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_SCREEN_SYS_SCREEN_PARENT_SCREEN_ID",
                        column: x => x.PARENT_SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SYS_SCREEN_SYS_SYSTEM_SYSTEM_ID",
                        column: x => x.SYSTEM_ID,
                        principalTable: "SYS_SYSTEM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SYS_TICKET_TYPE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TYPE_NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    TYPE_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DESCRIPTION_AR = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DESCRIPTION_EN = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DEFAULT_PRIORITY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SLA_TARGET_HOURS = table.Column<decimal>(type: "NUMBER", nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_TICKET_TYPE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_TICKET_TYPE_SYS_TICKET_PRIORITY_DEFAULT_PRIORITY_ID",
                        column: x => x.DEFAULT_PRIORITY_ID,
                        principalTable: "SYS_TICKET_PRIORITY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SYS_SAVED_SEARCH",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SEARCH_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SEARCH_DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    SEARCH_CRITERIA = table.Column<string>(type: "CLOB", nullable: false),
                    IS_PUBLIC = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    IS_DEFAULT = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    USAGE_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LAST_USED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SAVED_SEARCH", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_SAVED_SEARCH_SYS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SYS_USERS_ROLES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ROLE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ASSIGNED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ASSIGNED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_USERS_ROLES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_USERS_ROLES_SYS_ROLE_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalTable: "SYS_ROLE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_USERS_ROLES_SYS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SYS_ROLE_SCREEN_PERMISSIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ROLE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CAN_VIEW = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_INSERT = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_UPDATE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_DELETE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_ROLE_SCREEN_PERMISSIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_ROLE_SCREEN_PERMISSIONS_SYS_ROLE_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalTable: "SYS_ROLE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SYS_ROLE_SCREEN_PERMISSIONS_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SYS_USER_SCREEN_PERMISSIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CAN_VIEW = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_INSERT = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_UPDATE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_DELETE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    ASSIGNED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    AssignedDate = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_USER_SCREEN_PERMISSIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_USER_SCREEN_PERMISSIONS_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SYS_USER_SCREEN_PERMISSIONS_SYS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SYS_BRANCH",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PHONE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    MOBILE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    FAX = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    IS_HEAD_BRANCH = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    DEFAULT_LANG = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    BASE_CURRENCY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ROUNDING_RULES = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    BRANCH_LOGO = table.Column<string>(type: "CLOB", nullable: true),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_BRANCH", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYS_CURRENCY_BASE_CURRENCY_ID",
                        column: x => x.BASE_CURRENCY_ID,
                        principalTable: "SYS_CURRENCY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SYS_BRANCH_SCREEN_PERMISSIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CAN_VIEW = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_INSERT = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_UPDATE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_DELETE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_BRANCH_SCREEN_PERMISSIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SCREEN_PERMISSIONS_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SCREEN_PERMISSIONS_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SCREEN_PERMISSIONS_SYS_SUPER_ADMIN_GRANTED_BY",
                        column: x => x.GRANTED_BY,
                        principalTable: "SYS_SUPER_ADMIN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SYS_BRANCH_SYSTEMS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SYSTEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    IS_ALLOWED = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REVOKED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_BRANCH_SYSTEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYSTEMS_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYSTEMS_SYS_SUPER_ADMIN_GRANTED_BY",
                        column: x => x.GRANTED_BY,
                        principalTable: "SYS_SUPER_ADMIN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYSTEMS_SYS_SYSTEM_SYSTEM_ID",
                        column: x => x.SYSTEM_ID,
                        principalTable: "SYS_SYSTEM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SYS_COMPANY",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    COUNTRY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CURR_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    LEGAL_NAME = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    LEGAL_NAME_E = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    COMPANY_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TAX_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    DEFAULT_BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COMPANY_LOGO = table.Column<string>(type: "CLOB", nullable: true),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_COMPANY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SYS_BRANCH_DEFAULT_BRANCH_ID",
                        column: x => x.DEFAULT_BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SYS_CURRENCY_CURR_ID",
                        column: x => x.CURR_ID,
                        principalTable: "SYS_CURRENCY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SYS_COMPANY_SCREEN_PERMISSIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CAN_VIEW = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_INSERT = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_UPDATE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_DELETE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_COMPANY_SCREEN_PERMISSIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SCREEN_PERMISSIONS_SYS_COMPANY_COMPANY_ID",
                        column: x => x.COMPANY_ID,
                        principalTable: "SYS_COMPANY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SCREEN_PERMISSIONS_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SCREEN_PERMISSIONS_SYS_SUPER_ADMIN_GRANTED_BY",
                        column: x => x.GRANTED_BY,
                        principalTable: "SYS_SUPER_ADMIN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SYS_FISCAL_YEAR",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    FISCAL_YEAR_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    START_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    END_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    IS_CLOSED = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_FISCAL_YEAR", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_FISCAL_YEAR_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_FISCAL_YEAR_SYS_COMPANY_COMPANY_ID",
                        column: x => x.COMPANY_ID,
                        principalTable: "SYS_COMPANY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SYS_REQUEST_TICKET",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TITLE_AR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    TITLE_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NCLOB", maxLength: 5000, nullable: false),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    REQUESTER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ASSIGNEE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    TICKET_TYPE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TICKET_STATUS_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TICKET_PRIORITY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TICKET_CATEGORY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    EXPECTED_RESOLUTION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ACTUAL_RESOLUTION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IS_ACTIVE = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_REQUEST_TICKET", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_COMPANY_COMPANY_ID",
                        column: x => x.COMPANY_ID,
                        principalTable: "SYS_COMPANY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_TICKET_CATEGORY_TICKET_CATEGORY_ID",
                        column: x => x.TICKET_CATEGORY_ID,
                        principalTable: "SYS_TICKET_CATEGORY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_TICKET_PRIORITY_TICKET_PRIORITY_ID",
                        column: x => x.TICKET_PRIORITY_ID,
                        principalTable: "SYS_TICKET_PRIORITY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_TICKET_STATUS_TICKET_STATUS_ID",
                        column: x => x.TICKET_STATUS_ID,
                        principalTable: "SYS_TICKET_STATUS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_TICKET_TYPE_TICKET_TYPE_ID",
                        column: x => x.TICKET_TYPE_ID,
                        principalTable: "SYS_TICKET_TYPE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_USERS_ASSIGNEE_ID",
                        column: x => x.ASSIGNEE_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SYS_REQUEST_TICKET_SYS_USERS_REQUESTER_ID",
                        column: x => x.REQUESTER_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SYS_SEARCH_ANALYTICS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SEARCH_TERM = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    SEARCH_CRITERIA = table.Column<string>(type: "CLOB", nullable: true),
                    FILTER_LOGIC = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    RESULT_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EXECUTION_TIME_MS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SEARCH_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SEARCH_ANALYTICS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_SEARCH_ANALYTICS_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SYS_SEARCH_ANALYTICS_SYS_COMPANY_COMPANY_ID",
                        column: x => x.COMPANY_ID,
                        principalTable: "SYS_COMPANY",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SYS_SEARCH_ANALYTICS_SYS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SYS_TICKET_ATTACHMENT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TICKET_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    FILE_NAME = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                    FILE_SIZE = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    MIME_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    FILE_CONTENT = table.Column<string>(type: "CLOB", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_TICKET_ATTACHMENT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_TICKET_ATTACHMENT_SYS_REQUEST_TICKET_TICKET_ID",
                        column: x => x.TICKET_ID,
                        principalTable: "SYS_REQUEST_TICKET",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SYS_TICKET_COMMENT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TICKET_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    COMMENT_TEXT = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    IS_INTERNAL = table.Column<string>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_TICKET_COMMENT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_TICKET_COMMENT_SYS_REQUEST_TICKET_TICKET_ID",
                        column: x => x.TICKET_ID,
                        principalTable: "SYS_REQUEST_TICKET",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_BASE_CURRENCY_ID",
                table: "SYS_BRANCH",
                column: "BASE_CURRENCY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_COMPANY_ID",
                table: "SYS_BRANCH",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREEN_PERMISSIONS_BRANCH_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREEN_PERMISSIONS_GRANTED_BY",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS",
                column: "GRANTED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREEN_PERMISSIONS_SCREEN_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS",
                column: "SCREEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_BRANCH_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_GRANTED_BY",
                table: "SYS_BRANCH_SYSTEMS",
                column: "GRANTED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_SYSTEM_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "SYSTEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_CURR_ID",
                table: "SYS_COMPANY",
                column: "CURR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_DEFAULT_BRANCH_ID",
                table: "SYS_COMPANY",
                column: "DEFAULT_BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_SCREEN_PERMISSIONS_COMPANY_ID",
                table: "SYS_COMPANY_SCREEN_PERMISSIONS",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_SCREEN_PERMISSIONS_GRANTED_BY",
                table: "SYS_COMPANY_SCREEN_PERMISSIONS",
                column: "GRANTED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_SCREEN_PERMISSIONS_SCREEN_ID",
                table: "SYS_COMPANY_SCREEN_PERMISSIONS",
                column: "SCREEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_FISCAL_YEAR_BRANCH_ID",
                table: "SYS_FISCAL_YEAR",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_FISCAL_YEAR_COMPANY_ID",
                table: "SYS_FISCAL_YEAR",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_ASSIGNEE_ID",
                table: "SYS_REQUEST_TICKET",
                column: "ASSIGNEE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_BRANCH_ID",
                table: "SYS_REQUEST_TICKET",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_COMPANY_ID",
                table: "SYS_REQUEST_TICKET",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_REQUESTER_ID",
                table: "SYS_REQUEST_TICKET",
                column: "REQUESTER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_TICKET_CATEGORY_ID",
                table: "SYS_REQUEST_TICKET",
                column: "TICKET_CATEGORY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_TICKET_PRIORITY_ID",
                table: "SYS_REQUEST_TICKET",
                column: "TICKET_PRIORITY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_TICKET_STATUS_ID",
                table: "SYS_REQUEST_TICKET",
                column: "TICKET_STATUS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_REQUEST_TICKET_TICKET_TYPE_ID",
                table: "SYS_REQUEST_TICKET",
                column: "TICKET_TYPE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_ROLE_SCREEN_PERMISSIONS_ROLE_ID",
                table: "SYS_ROLE_SCREEN_PERMISSIONS",
                column: "ROLE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_ROLE_SCREEN_PERMISSIONS_SCREEN_ID",
                table: "SYS_ROLE_SCREEN_PERMISSIONS",
                column: "SCREEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SAVED_SEARCH_USER_ID",
                table: "SYS_SAVED_SEARCH",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SCREEN_PARENT_SCREEN_ID",
                table: "SYS_SCREEN",
                column: "PARENT_SCREEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SCREEN_SCREEN_CODE",
                table: "SYS_SCREEN",
                column: "SCREEN_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SCREEN_SYSTEM_ID",
                table: "SYS_SCREEN",
                column: "SYSTEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SEARCH_ANALYTICS_BRANCH_ID",
                table: "SYS_SEARCH_ANALYTICS",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SEARCH_ANALYTICS_COMPANY_ID",
                table: "SYS_SEARCH_ANALYTICS",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SEARCH_ANALYTICS_USER_ID",
                table: "SYS_SEARCH_ANALYTICS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SUPER_ADMIN_EMAIL",
                table: "SYS_SUPER_ADMIN",
                column: "EMAIL",
                unique: true,
                filter: "\"EMAIL\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SUPER_ADMIN_USER_NAME",
                table: "SYS_SUPER_ADMIN",
                column: "USER_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SYSTEM_SYSTEM_CODE",
                table: "SYS_SYSTEM",
                column: "SYSTEM_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_TICKET_ATTACHMENT_TICKET_ID",
                table: "SYS_TICKET_ATTACHMENT",
                column: "TICKET_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_TICKET_COMMENT_TICKET_ID",
                table: "SYS_TICKET_COMMENT",
                column: "TICKET_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_TICKET_CONFIG_CONFIG_KEY",
                table: "SYS_TICKET_CONFIG",
                column: "CONFIG_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_TICKET_STATUS_STATUS_CODE",
                table: "SYS_TICKET_STATUS",
                column: "STATUS_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_TICKET_TYPE_DEFAULT_PRIORITY_ID",
                table: "SYS_TICKET_TYPE",
                column: "DEFAULT_PRIORITY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USER_SCREEN_PERMISSIONS_SCREEN_ID",
                table: "SYS_USER_SCREEN_PERMISSIONS",
                column: "SCREEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USER_SCREEN_PERMISSIONS_USER_ID",
                table: "SYS_USER_SCREEN_PERMISSIONS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USERS_USER_NAME",
                table: "SYS_USERS",
                column: "USER_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USERS_ROLES_ROLE_ID",
                table: "SYS_USERS_ROLES",
                column: "ROLE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_USERS_ROLES_USER_ID",
                table: "SYS_USERS_ROLES",
                column: "USER_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_SYS_BRANCH_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH",
                column: "COMPANY_ID",
                principalTable: "SYS_COMPANY",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SYS_BRANCH_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH");

            migrationBuilder.DropTable(
                name: "SYS_AUDIT_LOG");

            migrationBuilder.DropTable(
                name: "SYS_AUDIT_LOG_ARCHIVE");

            migrationBuilder.DropTable(
                name: "SYS_BRANCH_SCREEN_PERMISSIONS");

            migrationBuilder.DropTable(
                name: "SYS_BRANCH_SYSTEMS");

            migrationBuilder.DropTable(
                name: "SYS_COMPANY_SCREEN_PERMISSIONS");

            migrationBuilder.DropTable(
                name: "SYS_COMPANY_SYSTEMS");

            migrationBuilder.DropTable(
                name: "SYS_FAILED_LOGINS");

            migrationBuilder.DropTable(
                name: "SYS_FISCAL_YEAR");

            migrationBuilder.DropTable(
                name: "SYS_PERFORMANCE_METRICS");

            migrationBuilder.DropTable(
                name: "SYS_REPORT_SCHEDULE");

            migrationBuilder.DropTable(
                name: "SYS_RETENTION_POLICIES");

            migrationBuilder.DropTable(
                name: "SYS_ROLE_SCREEN_PERMISSIONS");

            migrationBuilder.DropTable(
                name: "SYS_SAVED_SEARCH");

            migrationBuilder.DropTable(
                name: "SYS_SEARCH_ANALYTICS");

            migrationBuilder.DropTable(
                name: "SYS_SECURITY_THREATS");

            migrationBuilder.DropTable(
                name: "SYS_SLOW_QUERIES");

            migrationBuilder.DropTable(
                name: "SYS_TICKET_ATTACHMENT");

            migrationBuilder.DropTable(
                name: "SYS_TICKET_COMMENT");

            migrationBuilder.DropTable(
                name: "SYS_TICKET_CONFIG");

            migrationBuilder.DropTable(
                name: "SYS_USER_SCREEN_PERMISSIONS");

            migrationBuilder.DropTable(
                name: "SYS_USERS_ROLES");

            migrationBuilder.DropTable(
                name: "SYS_SUPER_ADMIN");

            migrationBuilder.DropTable(
                name: "SYS_REQUEST_TICKET");

            migrationBuilder.DropTable(
                name: "SYS_SCREEN");

            migrationBuilder.DropTable(
                name: "SYS_ROLE");

            migrationBuilder.DropTable(
                name: "SYS_TICKET_CATEGORY");

            migrationBuilder.DropTable(
                name: "SYS_TICKET_STATUS");

            migrationBuilder.DropTable(
                name: "SYS_TICKET_TYPE");

            migrationBuilder.DropTable(
                name: "SYS_USERS");

            migrationBuilder.DropTable(
                name: "SYS_SYSTEM");

            migrationBuilder.DropTable(
                name: "SYS_TICKET_PRIORITY");

            migrationBuilder.DropTable(
                name: "SYS_COMPANY");

            migrationBuilder.DropTable(
                name: "SYS_BRANCH");

            migrationBuilder.DropTable(
                name: "SYS_CURRENCY");
        }
    }
}
