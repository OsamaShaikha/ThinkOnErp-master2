using Oracle.ManagedDataAccess.Client;

internal static class HrEosShortLeaveSchema
{
    private static readonly (string Table, string Name, string Kind, int? Length, string Definition)[] OptionalColumns =
    [
        ("HR_END_OF_SERVICE_POLICY", "EFFECTIVE_FROM", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_END_OF_SERVICE_POLICY", "EFFECTIVE_TO", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_END_OF_SERVICE_POLICY", "UPDATE_USER", "TEXT", 200, "NVARCHAR2(200) NULL"),
        ("HR_END_OF_SERVICE_POLICY", "UPDATE_DATE", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "BRANCH_ID", "NUMBER", null, "NUMBER(19) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "TERMINATION_REASON", "TEXT", 1000, "NVARCHAR2(1000) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "NOTICE_DATE", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "CALCULATION_NOTES", "TEXT", 2000, "NVARCHAR2(2000) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "APPROVED_BY", "TEXT", 200, "NVARCHAR2(200) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "APPROVED_AT_UTC", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "CANCELLED_BY", "TEXT", 200, "NVARCHAR2(200) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "CANCELLED_AT_UTC", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "CANCELLATION_REASON", "TEXT", 1000, "NVARCHAR2(1000) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "UPDATE_USER", "TEXT", 200, "NVARCHAR2(200) NULL"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "UPDATE_DATE", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_EOS_SETTLEMENT_LINE", "DESCRIPTION", "TEXT", 500, "NVARCHAR2(500) NULL"),
        ("HR_SHORT_LEAVE_POLICY", "UPDATE_USER", "TEXT", 100, "NVARCHAR2(100) NULL"),
        ("HR_SHORT_LEAVE_POLICY", "UPDATE_DATE", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "BRANCH_ID", "NUMBER", null, "NUMBER(19) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "APPROVED_BY", "TEXT", 100, "NVARCHAR2(100) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "APPROVED_AT_UTC", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "REJECTED_BY", "TEXT", 100, "NVARCHAR2(100) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "REJECTED_AT_UTC", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "REJECTION_REASON", "TEXT", 500, "NVARCHAR2(500) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "CANCELLED_BY", "TEXT", 100, "NVARCHAR2(100) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "CANCELLED_AT_UTC", "TIMESTAMP", null, "TIMESTAMP(7) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "CANCELLATION_REASON", "TEXT", 500, "NVARCHAR2(500) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "UPDATE_USER", "TEXT", 100, "NVARCHAR2(100) NULL"),
        ("HR_SHORT_LEAVE_REQUEST", "UPDATE_DATE", "TIMESTAMP", null, "TIMESTAMP(7) NULL")
    ];

    private static readonly IReadOnlyDictionary<string, string[]> RequiredMappedColumns = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["HR_END_OF_SERVICE_POLICY"] = ["ID", "COMPANY_ID", "POLICY_NAME", "CALCULATION_BASIS", "LEAVE_DAILY_RATE_DIVISOR", "NOTICE_PERIOD_DAYS", "PRORATE_BY_WORKING_DAYS", "RESIGN_FACTOR_UNDER_2Y", "RESIGN_FACTOR_2_TO_5Y", "RESIGN_FACTOR_5_TO_10Y", "RESIGN_FACTOR_10Y_PLUS", "FIRST_5Y_RATE_PER_YEAR", "SUBSEQUENT_RATE_PER_YEAR", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"],
        ["HR_END_OF_SERVICE_SETTLEMENT"] = ["ID", "COMPANY_ID", "EMPLOYEE_CODE", "TERMINATION_TYPE", "LAST_WORKING_DATE", "SETTLEMENT_DATE", "HIRE_DATE", "SERVICE_YEARS", "SERVICE_MONTHS", "SERVICE_DAYS", "BASE_SALARY_SNAPSHOT", "GROSS_SALARY_SNAPSHOT", "CURRENCY_CODE", "SEVERANCE_PAY", "UNPAID_SALARY", "LEAVE_ENCASHMENT", "UNUSED_LEAVE_DAYS", "NOTICE_PERIOD_PAY", "OTHER_ENTITLEMENTS", "TOTAL_ENTITLEMENTS", "OUTSTANDING_LOANS", "OUTSTANDING_ADVANCES", "NEGATIVE_LEAVE_DEDUCTION", "OTHER_DEDUCTIONS", "TOTAL_DEDUCTIONS", "NET_SETTLEMENT_AMOUNT", "STATUS", "CREATION_USER", "CREATION_DATE"],
        ["HR_EOS_SETTLEMENT_LINE"] = ["ID", "SETTLEMENT_ID", "COMPONENT_CODE", "COMPONENT_TYPE", "AMOUNT"],
        ["HR_SHORT_LEAVE_POLICY"] = ["ID", "COMPANY_ID", "MONTHLY_PAID_MINUTES_QUOTA", "MAX_MINUTES_PER_REQUEST", "ALLOW_MIDDAY_EXIT", "EXCESS_ACTION", "LEAVE_TYPE_CODE_FOR_DEDUCTION", "STANDARD_WORKDAY_HOURS", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"],
        ["HR_SHORT_LEAVE_REQUEST"] = ["ID", "COMPANY_ID", "EMPLOYEE_CODE", "PERMISSION_TYPE", "PERMISSION_DATE", "START_TIME", "END_TIME", "DURATION_MINUTES", "REASON", "STATUS", "COMPENSATION_TYPE", "DEDUCTED_SALARY_AMOUNT", "CONVERTED_LEAVE_DAYS", "CREATION_USER", "CREATION_DATE"]
    };

    private static readonly (string Table, string Column, int Precision, int Scale)[] RequiredNumberColumns =
    [
        ("HR_END_OF_SERVICE_POLICY", "ID", 19, 0), ("HR_END_OF_SERVICE_POLICY", "COMPANY_ID", 19, 0),
        ("HR_END_OF_SERVICE_POLICY", "LEAVE_DAILY_RATE_DIVISOR", 8, 2), ("HR_END_OF_SERVICE_POLICY", "NOTICE_PERIOD_DAYS", 10, 0),
        ("HR_END_OF_SERVICE_POLICY", "PRORATE_BY_WORKING_DAYS", 1, 0), ("HR_END_OF_SERVICE_POLICY", "RESIGN_FACTOR_UNDER_2Y", 6, 4),
        ("HR_END_OF_SERVICE_POLICY", "RESIGN_FACTOR_2_TO_5Y", 6, 4), ("HR_END_OF_SERVICE_POLICY", "RESIGN_FACTOR_5_TO_10Y", 6, 4),
        ("HR_END_OF_SERVICE_POLICY", "RESIGN_FACTOR_10Y_PLUS", 6, 4), ("HR_END_OF_SERVICE_POLICY", "FIRST_5Y_RATE_PER_YEAR", 6, 4),
        ("HR_END_OF_SERVICE_POLICY", "SUBSEQUENT_RATE_PER_YEAR", 6, 4), ("HR_END_OF_SERVICE_POLICY", "IS_ACTIVE", 1, 0),
        ("HR_END_OF_SERVICE_SETTLEMENT", "ID", 19, 0), ("HR_END_OF_SERVICE_SETTLEMENT", "COMPANY_ID", 19, 0),
        ("HR_END_OF_SERVICE_SETTLEMENT", "SERVICE_YEARS", 10, 4), ("HR_END_OF_SERVICE_SETTLEMENT", "SERVICE_MONTHS", 10, 0),
        ("HR_END_OF_SERVICE_SETTLEMENT", "SERVICE_DAYS", 10, 0), ("HR_END_OF_SERVICE_SETTLEMENT", "BASE_SALARY_SNAPSHOT", 18, 3),
        ("HR_END_OF_SERVICE_SETTLEMENT", "GROSS_SALARY_SNAPSHOT", 18, 3), ("HR_END_OF_SERVICE_SETTLEMENT", "SEVERANCE_PAY", 18, 3),
        ("HR_END_OF_SERVICE_SETTLEMENT", "UNPAID_SALARY", 18, 3), ("HR_END_OF_SERVICE_SETTLEMENT", "LEAVE_ENCASHMENT", 18, 3),
        ("HR_END_OF_SERVICE_SETTLEMENT", "UNUSED_LEAVE_DAYS", 10, 4), ("HR_END_OF_SERVICE_SETTLEMENT", "NOTICE_PERIOD_PAY", 18, 3),
        ("HR_END_OF_SERVICE_SETTLEMENT", "OTHER_ENTITLEMENTS", 18, 3), ("HR_END_OF_SERVICE_SETTLEMENT", "TOTAL_ENTITLEMENTS", 18, 3),
        ("HR_END_OF_SERVICE_SETTLEMENT", "OUTSTANDING_LOANS", 18, 3), ("HR_END_OF_SERVICE_SETTLEMENT", "OUTSTANDING_ADVANCES", 18, 3),
        ("HR_END_OF_SERVICE_SETTLEMENT", "NEGATIVE_LEAVE_DEDUCTION", 18, 3), ("HR_END_OF_SERVICE_SETTLEMENT", "OTHER_DEDUCTIONS", 18, 3),
        ("HR_END_OF_SERVICE_SETTLEMENT", "TOTAL_DEDUCTIONS", 18, 3), ("HR_END_OF_SERVICE_SETTLEMENT", "NET_SETTLEMENT_AMOUNT", 18, 3),
        ("HR_EOS_SETTLEMENT_LINE", "ID", 19, 0), ("HR_EOS_SETTLEMENT_LINE", "SETTLEMENT_ID", 19, 0),
        ("HR_EOS_SETTLEMENT_LINE", "AMOUNT", 18, 3), ("HR_SHORT_LEAVE_POLICY", "ID", 19, 0),
        ("HR_SHORT_LEAVE_POLICY", "COMPANY_ID", 19, 0), ("HR_SHORT_LEAVE_POLICY", "MONTHLY_PAID_MINUTES_QUOTA", 10, 0),
        ("HR_SHORT_LEAVE_POLICY", "MAX_MINUTES_PER_REQUEST", 10, 0), ("HR_SHORT_LEAVE_POLICY", "ALLOW_MIDDAY_EXIT", 1, 0),
        ("HR_SHORT_LEAVE_POLICY", "STANDARD_WORKDAY_HOURS", 5, 2), ("HR_SHORT_LEAVE_POLICY", "IS_ACTIVE", 1, 0),
        ("HR_SHORT_LEAVE_REQUEST", "ID", 19, 0), ("HR_SHORT_LEAVE_REQUEST", "COMPANY_ID", 19, 0),
        ("HR_SHORT_LEAVE_REQUEST", "DURATION_MINUTES", 10, 0), ("HR_SHORT_LEAVE_REQUEST", "DEDUCTED_SALARY_AMOUNT", 18, 4),
        ("HR_SHORT_LEAVE_REQUEST", "CONVERTED_LEAVE_DAYS", 8, 4)
    ];

    private static readonly (string Table, string Column)[] RequiredTimestampColumns =
    [
        ("HR_END_OF_SERVICE_POLICY", "CREATION_DATE"), ("HR_END_OF_SERVICE_SETTLEMENT", "LAST_WORKING_DATE"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "SETTLEMENT_DATE"), ("HR_END_OF_SERVICE_SETTLEMENT", "HIRE_DATE"),
        ("HR_END_OF_SERVICE_SETTLEMENT", "CREATION_DATE"),
        ("HR_SHORT_LEAVE_POLICY", "CREATION_DATE"), ("HR_SHORT_LEAVE_REQUEST", "PERMISSION_DATE"),
        ("HR_SHORT_LEAVE_REQUEST", "CREATION_DATE")
    ];

    private static readonly (string Table, string Column)[] RequiredIntervalColumns =
    [
        ("HR_SHORT_LEAVE_REQUEST", "START_TIME"), ("HR_SHORT_LEAVE_REQUEST", "END_TIME")
    ];

    private static readonly (string Table, string Column, int Length)[] RequiredTextColumns =
    [
        ("HR_END_OF_SERVICE_POLICY", "POLICY_NAME", 200), ("HR_END_OF_SERVICE_POLICY", "CALCULATION_BASIS", 50),
        ("HR_END_OF_SERVICE_POLICY", "CREATION_USER", 200),
        ("HR_END_OF_SERVICE_SETTLEMENT", "EMPLOYEE_CODE", 100), ("HR_END_OF_SERVICE_SETTLEMENT", "TERMINATION_TYPE", 50),
        ("HR_END_OF_SERVICE_SETTLEMENT", "CURRENCY_CODE", 20), ("HR_END_OF_SERVICE_SETTLEMENT", "STATUS", 50),
        ("HR_END_OF_SERVICE_SETTLEMENT", "CREATION_USER", 200),
        ("HR_EOS_SETTLEMENT_LINE", "COMPONENT_CODE", 50), ("HR_EOS_SETTLEMENT_LINE", "COMPONENT_TYPE", 50),
        ("HR_SHORT_LEAVE_POLICY", "EXCESS_ACTION", 50), ("HR_SHORT_LEAVE_POLICY", "LEAVE_TYPE_CODE_FOR_DEDUCTION", 50),
        ("HR_SHORT_LEAVE_POLICY", "CREATION_USER", 100),
        ("HR_SHORT_LEAVE_REQUEST", "EMPLOYEE_CODE", 50), ("HR_SHORT_LEAVE_REQUEST", "PERMISSION_TYPE", 50),
        ("HR_SHORT_LEAVE_REQUEST", "REASON", 500), ("HR_SHORT_LEAVE_REQUEST", "STATUS", 50),
        ("HR_SHORT_LEAVE_REQUEST", "COMPENSATION_TYPE", 50), ("HR_SHORT_LEAVE_REQUEST", "CREATION_USER", 100)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await ValidateAttendanceMultipleSessionsColumn(connection, allowMissingColumn: true);
        await PreflightRequiredMappedColumns(connection);
        await ValidateRequiredTextColumnDefinitions(connection, requireTables: false);
        await PreflightScalarColumnDefinitions(connection);
        foreach (var column in OptionalColumns)
            await ValidateNullableColumn(connection, column.Table, column.Name, column.Kind, column.Length);
        await PreflightIndexesAndConstraints(connection);
        await PreflightForeignKeyData(connection);

        await EnsureTable(connection, "HR_END_OF_SERVICE_POLICY", """
            CREATE TABLE DEV_TEMPLATE.HR_END_OF_SERVICE_POLICY (
                ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY,
                COMPANY_ID NUMBER(19) NOT NULL,
                EFFECTIVE_FROM TIMESTAMP(7) NULL,
                EFFECTIVE_TO TIMESTAMP(7) NULL,
                POLICY_NAME NVARCHAR2(200) NOT NULL,
                CALCULATION_BASIS NVARCHAR2(50) NOT NULL,
                LEAVE_DAILY_RATE_DIVISOR NUMBER(8,2) NOT NULL,
                NOTICE_PERIOD_DAYS NUMBER(10) NOT NULL,
                PRORATE_BY_WORKING_DAYS NUMBER(1) NOT NULL,
                RESIGN_FACTOR_UNDER_2Y NUMBER(6,4) NOT NULL,
                RESIGN_FACTOR_2_TO_5Y NUMBER(6,4) NOT NULL,
                RESIGN_FACTOR_5_TO_10Y NUMBER(6,4) NOT NULL,
                RESIGN_FACTOR_10Y_PLUS NUMBER(6,4) NOT NULL,
                FIRST_5Y_RATE_PER_YEAR NUMBER(6,4) NOT NULL,
                SUBSEQUENT_RATE_PER_YEAR NUMBER(6,4) NOT NULL,
                IS_ACTIVE NUMBER(1) NOT NULL,
                CREATION_USER NVARCHAR2(200) NOT NULL,
                CREATION_DATE TIMESTAMP(7) NOT NULL,
                UPDATE_USER NVARCHAR2(200) NULL,
                UPDATE_DATE TIMESTAMP(7) NULL
            )
            """, report);
        await EnsureIndex(connection, "IX_HR_EOS_POL_COMP", "CREATE INDEX DEV_TEMPLATE.IX_HR_EOS_POL_COMP ON DEV_TEMPLATE.HR_END_OF_SERVICE_POLICY (COMPANY_ID, IS_ACTIVE)", report);

        await EnsureTable(connection, "HR_END_OF_SERVICE_SETTLEMENT", """
            CREATE TABLE DEV_TEMPLATE.HR_END_OF_SERVICE_SETTLEMENT (
                ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY,
                COMPANY_ID NUMBER(19) NOT NULL,
                BRANCH_ID NUMBER(19) NULL,
                EMPLOYEE_CODE NVARCHAR2(100) NOT NULL,
                TERMINATION_TYPE NVARCHAR2(50) NOT NULL,
                TERMINATION_REASON NVARCHAR2(1000) NULL,
                NOTICE_DATE TIMESTAMP(7) NULL,
                LAST_WORKING_DATE TIMESTAMP(7) NOT NULL,
                SETTLEMENT_DATE TIMESTAMP(7) NOT NULL,
                HIRE_DATE TIMESTAMP(7) NOT NULL,
                SERVICE_YEARS NUMBER(10,4) NOT NULL,
                SERVICE_MONTHS NUMBER(10) NOT NULL,
                SERVICE_DAYS NUMBER(10) NOT NULL,
                BASE_SALARY_SNAPSHOT NUMBER(18,3) NOT NULL,
                GROSS_SALARY_SNAPSHOT NUMBER(18,3) NOT NULL,
                CURRENCY_CODE NVARCHAR2(20) NOT NULL,
                SEVERANCE_PAY NUMBER(18,3) NOT NULL,
                UNPAID_SALARY NUMBER(18,3) NOT NULL,
                LEAVE_ENCASHMENT NUMBER(18,3) NOT NULL,
                UNUSED_LEAVE_DAYS NUMBER(10,4) NOT NULL,
                NOTICE_PERIOD_PAY NUMBER(18,3) NOT NULL,
                OTHER_ENTITLEMENTS NUMBER(18,3) NOT NULL,
                TOTAL_ENTITLEMENTS NUMBER(18,3) NOT NULL,
                OUTSTANDING_LOANS NUMBER(18,3) NOT NULL,
                OUTSTANDING_ADVANCES NUMBER(18,3) NOT NULL,
                NEGATIVE_LEAVE_DEDUCTION NUMBER(18,3) NOT NULL,
                OTHER_DEDUCTIONS NUMBER(18,3) NOT NULL,
                TOTAL_DEDUCTIONS NUMBER(18,3) NOT NULL,
                NET_SETTLEMENT_AMOUNT NUMBER(18,3) NOT NULL,
                STATUS NVARCHAR2(50) NOT NULL,
                CALCULATION_NOTES NVARCHAR2(2000) NULL,
                APPROVED_BY NVARCHAR2(200) NULL,
                APPROVED_AT_UTC TIMESTAMP(7) NULL,
                CANCELLED_BY NVARCHAR2(200) NULL,
                CANCELLED_AT_UTC TIMESTAMP(7) NULL,
                CANCELLATION_REASON NVARCHAR2(1000) NULL,
                CREATION_USER NVARCHAR2(200) NOT NULL,
                CREATION_DATE TIMESTAMP(7) NOT NULL,
                UPDATE_USER NVARCHAR2(200) NULL,
                UPDATE_DATE TIMESTAMP(7) NULL
            )
            """, report);
        await EnsureConstraint(connection, "FK_HR_EOS_EMPLOYEE",
            "ALTER TABLE DEV_TEMPLATE.HR_END_OF_SERVICE_SETTLEMENT ADD CONSTRAINT FK_HR_EOS_EMPLOYEE FOREIGN KEY (EMPLOYEE_CODE) REFERENCES DEV_TEMPLATE.HR_EMPLOYEE(EMPLOYEE_CODE)", report);
        await EnsureIndex(connection, "IX_HR_EOS_EMP_STATUS", "CREATE INDEX DEV_TEMPLATE.IX_HR_EOS_EMP_STATUS ON DEV_TEMPLATE.HR_END_OF_SERVICE_SETTLEMENT (COMPANY_ID, EMPLOYEE_CODE, STATUS)", report);

        await EnsureTable(connection, "HR_EOS_SETTLEMENT_LINE", """
            CREATE TABLE DEV_TEMPLATE.HR_EOS_SETTLEMENT_LINE (
                ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY,
                SETTLEMENT_ID NUMBER(19) NOT NULL,
                COMPONENT_CODE NVARCHAR2(50) NOT NULL,
                COMPONENT_TYPE NVARCHAR2(50) NOT NULL,
                AMOUNT NUMBER(18,3) NOT NULL,
                DESCRIPTION NVARCHAR2(500) NULL,
                CONSTRAINT FK_HR_EOS_LINE_SETTLE FOREIGN KEY (SETTLEMENT_ID)
                    REFERENCES DEV_TEMPLATE.HR_END_OF_SERVICE_SETTLEMENT(ID) ON DELETE CASCADE
            )
            """, report);
        await EnsureConstraint(connection, "FK_HR_EOS_LINE_SETTLE",
            "ALTER TABLE DEV_TEMPLATE.HR_EOS_SETTLEMENT_LINE ADD CONSTRAINT FK_HR_EOS_LINE_SETTLE FOREIGN KEY (SETTLEMENT_ID) REFERENCES DEV_TEMPLATE.HR_END_OF_SERVICE_SETTLEMENT(ID) ON DELETE CASCADE", report);
        await EnsureIndex(connection, "IX_HR_EOS_LINE_SETTLE", "CREATE INDEX DEV_TEMPLATE.IX_HR_EOS_LINE_SETTLE ON DEV_TEMPLATE.HR_EOS_SETTLEMENT_LINE (SETTLEMENT_ID)", report);

        await EnsureTable(connection, "HR_SHORT_LEAVE_POLICY", """
            CREATE TABLE DEV_TEMPLATE.HR_SHORT_LEAVE_POLICY (
                ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY,
                COMPANY_ID NUMBER(19) NOT NULL,
                MONTHLY_PAID_MINUTES_QUOTA NUMBER(10) NOT NULL,
                MAX_MINUTES_PER_REQUEST NUMBER(10) NOT NULL,
                ALLOW_MIDDAY_EXIT NUMBER(1) NOT NULL,
                EXCESS_ACTION NVARCHAR2(50) NOT NULL,
                LEAVE_TYPE_CODE_FOR_DEDUCTION NVARCHAR2(50) NOT NULL,
                STANDARD_WORKDAY_HOURS NUMBER(5,2) NOT NULL,
                IS_ACTIVE NUMBER(1) NOT NULL,
                CREATION_USER NVARCHAR2(100) NOT NULL,
                CREATION_DATE TIMESTAMP(7) NOT NULL,
                UPDATE_USER NVARCHAR2(100) NULL,
                UPDATE_DATE TIMESTAMP(7) NULL
            )
            """, report);
        await EnsureIndex(connection, "IX_HR_SHORT_LEAVE_POLICY_COMP", "CREATE INDEX DEV_TEMPLATE.IX_HR_SHORT_LEAVE_POLICY_COMP ON DEV_TEMPLATE.HR_SHORT_LEAVE_POLICY (COMPANY_ID)", report);

        await EnsureTable(connection, "HR_SHORT_LEAVE_REQUEST", """
            CREATE TABLE DEV_TEMPLATE.HR_SHORT_LEAVE_REQUEST (
                ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY,
                COMPANY_ID NUMBER(19) NOT NULL,
                BRANCH_ID NUMBER(19) NULL,
                EMPLOYEE_CODE NVARCHAR2(50) NOT NULL,
                PERMISSION_TYPE NVARCHAR2(50) NOT NULL,
                PERMISSION_DATE TIMESTAMP(7) NOT NULL,
                START_TIME INTERVAL DAY(8) TO SECOND(7) NOT NULL,
                END_TIME INTERVAL DAY(8) TO SECOND(7) NOT NULL,
                DURATION_MINUTES NUMBER(10) NOT NULL,
                REASON NVARCHAR2(500) NOT NULL,
                STATUS NVARCHAR2(50) NOT NULL,
                COMPENSATION_TYPE NVARCHAR2(50) NOT NULL,
                DEDUCTED_SALARY_AMOUNT NUMBER(18,4) NOT NULL,
                CONVERTED_LEAVE_DAYS NUMBER(8,4) NOT NULL,
                APPROVED_BY NVARCHAR2(100) NULL,
                APPROVED_AT_UTC TIMESTAMP(7) NULL,
                REJECTED_BY NVARCHAR2(100) NULL,
                REJECTED_AT_UTC TIMESTAMP(7) NULL,
                REJECTION_REASON NVARCHAR2(500) NULL,
                CANCELLED_BY NVARCHAR2(100) NULL,
                CANCELLED_AT_UTC TIMESTAMP(7) NULL,
                CANCELLATION_REASON NVARCHAR2(500) NULL,
                CREATION_USER NVARCHAR2(100) NOT NULL,
                CREATION_DATE TIMESTAMP(7) NOT NULL,
                UPDATE_USER NVARCHAR2(100) NULL,
                UPDATE_DATE TIMESTAMP(7) NULL
            )
            """, report);
        foreach (var column in OptionalColumns)
            await EnsureNullableColumn(connection, column.Table, column.Name, column.Kind, column.Length, column.Definition, report);

        await EnsureConstraint(connection, "FK_HR_SHORT_LEAVE_EMPLOYEE",
            "ALTER TABLE DEV_TEMPLATE.HR_SHORT_LEAVE_REQUEST ADD CONSTRAINT FK_HR_SHORT_LEAVE_EMPLOYEE FOREIGN KEY (EMPLOYEE_CODE) REFERENCES DEV_TEMPLATE.HR_EMPLOYEE(EMPLOYEE_CODE)", report);
        await EnsureIndex(connection, "IX_HR_SHORT_LEAVE_EMP_DATE", "CREATE INDEX DEV_TEMPLATE.IX_HR_SHORT_LEAVE_EMP_DATE ON DEV_TEMPLATE.HR_SHORT_LEAVE_REQUEST (EMPLOYEE_CODE, PERMISSION_DATE)", report);
        await EnsureIndex(connection, "IX_HR_SHORT_LEAVE_COMP_STAT", "CREATE INDEX DEV_TEMPLATE.IX_HR_SHORT_LEAVE_COMP_STAT ON DEV_TEMPLATE.HR_SHORT_LEAVE_REQUEST (COMPANY_ID, STATUS)", report);

        await EnsureColumn(connection, "HR_ATTENDANCE_POLICY", "ALLOW_MULTIPLE_SESSIONS",
            "ALTER TABLE DEV_TEMPLATE.HR_ATTENDANCE_POLICY ADD (ALLOW_MULTIPLE_SESSIONS NUMBER(1) DEFAULT 0 NOT NULL)", report);
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await ValidateAttendanceMultipleSessionsColumn(connection, allowMissingColumn: false);
        foreach (var (table, requiredColumns) in RequiredMappedColumns)
        {
            var actual = await Query(connection, "SELECT COLUMN_NAME FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table",
                ("p_table", table));
            var found = actual.Select(row => row[0]).ToHashSet(StringComparer.Ordinal);
            var missing = requiredColumns.Where(column => !found.Contains(column)).ToArray();
            if (missing.Length != 0)
                throw new InvalidOperationException($"{table} is missing mapped columns: {string.Join(", ", missing)}.");
            await Execute(connection, $"SELECT * FROM DEV_TEMPLATE.{table} WHERE 1=0");
            report.Add($"{table}: required mapped columns present; mapped SELECT accepted.");
        }
        await ValidateRequiredTextColumnDefinitions(connection, requireTables: true);
        report.Add($"EOS and short-leave required text columns: {RequiredTextColumns.Length} definitions match EF.");
        foreach (var column in OptionalColumns)
            if (!await ValidateNullableColumn(connection, column.Table, column.Name, column.Kind, column.Length))
                throw new InvalidOperationException($"DEV_TEMPLATE.{column.Table}.{column.Name} is missing after installation.");
        report.Add($"EOS and short-leave optional columns: all {OptionalColumns.Length} definitions match the nullable EF mapping.");
        await ValidateScalarColumnDefinitions(connection, requireTables: true);
        report.Add($"EOS and short-leave required numeric/time columns: {RequiredNumberColumns.Length + RequiredTimestampColumns.Length + RequiredIntervalColumns.Length} metadata definitions match EF.");

        report.Add("HR_ATTENDANCE_POLICY.ALLOW_MULTIPLE_SESSIONS: present and NOT NULL.");

        var expectedIndexes = new Dictionary<string, (string Table, string[] Columns)>(StringComparer.Ordinal)
        {
            ["IX_HR_EOS_POL_COMP"] = ("HR_END_OF_SERVICE_POLICY", ["COMPANY_ID", "IS_ACTIVE"]),
            ["IX_HR_EOS_EMP_STATUS"] = ("HR_END_OF_SERVICE_SETTLEMENT", ["COMPANY_ID", "EMPLOYEE_CODE", "STATUS"]),
            ["IX_HR_EOS_LINE_SETTLE"] = ("HR_EOS_SETTLEMENT_LINE", ["SETTLEMENT_ID"]),
            ["IX_HR_SHORT_LEAVE_POLICY_COMP"] = ("HR_SHORT_LEAVE_POLICY", ["COMPANY_ID"]),
            ["IX_HR_SHORT_LEAVE_EMP_DATE"] = ("HR_SHORT_LEAVE_REQUEST", ["EMPLOYEE_CODE", "PERMISSION_DATE"]),
            ["IX_HR_SHORT_LEAVE_COMP_STAT"] = ("HR_SHORT_LEAVE_REQUEST", ["COMPANY_ID", "STATUS"])
        };
        foreach (var (index, expected) in expectedIndexes)
        {
            var actualColumns = await Query(connection,
                "SELECT i.TABLE_NAME,i.UNIQUENESS,i.STATUS,i.INDEX_TYPE,LISTAGG(c.COLUMN_NAME,',') WITHIN GROUP (ORDER BY c.COLUMN_POSITION) FROM ALL_INDEXES i JOIN ALL_IND_COLUMNS c ON c.INDEX_OWNER=i.OWNER AND c.INDEX_NAME=i.INDEX_NAME WHERE i.OWNER='DEV_TEMPLATE' AND i.INDEX_NAME=:p_name GROUP BY i.TABLE_NAME,i.UNIQUENESS,i.STATUS,i.INDEX_TYPE",
                ("p_name", index));
            if (actualColumns.Count != 1 || actualColumns[0][0] != expected.Table || actualColumns[0][1] != "NONUNIQUE" ||
                actualColumns[0][2] != "VALID" || actualColumns[0][3] != "NORMAL" ||
                !actualColumns[0][4].Split(',', StringSplitOptions.None).SequenceEqual(expected.Columns, StringComparer.Ordinal))
                throw new InvalidOperationException($"{index} is missing or has an unexpected table, state, type, uniqueness, or ordered column signature.");
        }
        report.Add("EOS and short-leave indexes: all six indexes match the EF table, state, type, uniqueness and ordered column definitions.");

        var employeeLinks = await Query(connection, """
            SELECT c.CONSTRAINT_NAME,c.TABLE_NAME,c.CONSTRAINT_TYPE,c.STATUS,c.VALIDATED,NVL(c.DELETE_RULE,'-'),
                   c.R_OWNER,r.TABLE_NAME,r.OWNER,r.CONSTRAINT_TYPE,r.STATUS,r.VALIDATED,cc.COLUMN_NAME,rc.COLUMN_NAME
            FROM ALL_CONSTRAINTS c
            JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME
            JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME
            JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME AND rc.POSITION=cc.POSITION
            WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME IN ('FK_HR_EOS_EMPLOYEE','FK_HR_SHORT_LEAVE_EMPLOYEE')
            ORDER BY c.CONSTRAINT_NAME
            """);
        var expectedEmployeeLinks = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["FK_HR_EOS_EMPLOYEE"] = ["HR_END_OF_SERVICE_SETTLEMENT", "R", "ENABLED", "VALIDATED", "NO ACTION", "DEV_TEMPLATE", "HR_EMPLOYEE", "DEV_TEMPLATE", "P", "ENABLED", "VALIDATED", "EMPLOYEE_CODE", "EMPLOYEE_CODE"],
            ["FK_HR_SHORT_LEAVE_EMPLOYEE"] = ["HR_SHORT_LEAVE_REQUEST", "R", "ENABLED", "VALIDATED", "NO ACTION", "DEV_TEMPLATE", "HR_EMPLOYEE", "DEV_TEMPLATE", "P", "ENABLED", "VALIDATED", "EMPLOYEE_CODE", "EMPLOYEE_CODE"]
        };
        if (employeeLinks.Count != expectedEmployeeLinks.Count || employeeLinks.Any(row =>
                !expectedEmployeeLinks.TryGetValue(row[0], out var expected) || !row.Skip(1).SequenceEqual(expected, StringComparer.Ordinal)))
            throw new InvalidOperationException("EOS/short-leave employee foreign keys are missing or do not match their exact child/parent table, owner, columns, delete rule and enabled validated state.");

        var lineLink = await Query(connection, """
            SELECT c.TABLE_NAME,c.CONSTRAINT_TYPE,c.STATUS,c.VALIDATED,c.DELETE_RULE,c.R_OWNER,r.TABLE_NAME,r.OWNER,
                   r.CONSTRAINT_TYPE,r.STATUS,r.VALIDATED,LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION),
                   LISTAGG(rc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION)
            FROM ALL_CONSTRAINTS c
            JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME
            JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME
            JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME AND rc.POSITION=cc.POSITION
            WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME='FK_HR_EOS_LINE_SETTLE'
            GROUP BY c.TABLE_NAME,c.CONSTRAINT_TYPE,c.STATUS,c.VALIDATED,c.DELETE_RULE,c.R_OWNER,r.TABLE_NAME,r.OWNER,r.CONSTRAINT_TYPE,r.STATUS,r.VALIDATED
            """);
        var expectedLineLink = new[] { "HR_EOS_SETTLEMENT_LINE", "R", "ENABLED", "VALIDATED", "CASCADE", "DEV_TEMPLATE", "HR_END_OF_SERVICE_SETTLEMENT", "DEV_TEMPLATE", "P", "ENABLED", "VALIDATED", "SETTLEMENT_ID", "ID" };
        if (lineLink.Count != 1 || !lineLink[0].SequenceEqual(expectedLineLink, StringComparer.Ordinal))
            throw new InvalidOperationException("EOS settlement-line FK is missing or does not match its exact child/parent key and cascade behavior.");
        report.Add("EOS/short-leave employee links and settlement-line cascade match their exact foreign-key mappings.");
    }

    private static async Task EnsureTable(OracleConnection connection, string table, string ddl, List<string> report)
    {
        var exists = await Scalar(connection, "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table", ("p_table", table));
        if (Convert.ToInt32(exists) != 0)
        {
            report.Add($"{table}: already exists; no table DDL applied.");
            return;
        }
        await Execute(connection, ddl);
        report.Add($"{table}: created from the current EF mapping.");
    }

    private static async Task EnsureColumn(OracleConnection connection, string table, string column, string ddl, List<string> report)
    {
        var exists = await Scalar(connection, "SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table AND COLUMN_NAME=:p_column",
            ("p_table", table), ("p_column", column));
        if (Convert.ToInt32(exists) != 0)
        {
            report.Add($"{table}.{column}: already exists; no column DDL applied.");
            return;
        }
        await Execute(connection, ddl);
        report.Add($"{table}.{column}: added with a false default for existing policies.");
    }

    private static async Task ValidateAttendanceMultipleSessionsColumn(OracleConnection connection, bool allowMissingColumn)
    {
        var tableExists = Convert.ToInt32(await Scalar(connection,
            "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_POLICY'")) == 1;
        if (!tableExists)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_ATTENDANCE_POLICY is missing; no EOS/short-leave installer DDL was applied.");

        var columnExists = Convert.ToInt32(await Scalar(connection,
            "SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_POLICY' AND COLUMN_NAME='ALLOW_MULTIPLE_SESSIONS'")) == 1;
        HrSchemaColumnExpectation expectation = new(
            "HR_ATTENDANCE_POLICY", "ALLOW_MULTIPLE_SESSIONS", "NUMBER", "N",
            Precision: 1, Scale: 0, Additive: true, DefaultExpression: "0");
        await HrSchemaColumnGuard.ValidateAsync(connection, [expectation],
            allowMissingColumns: allowMissingColumn && !columnExists);

        if (columnExists)
        {
            var invalidValues = Convert.ToInt64(await Scalar(connection,
                "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_ATTENDANCE_POLICY WHERE ALLOW_MULTIPLE_SESSIONS IS NULL OR ALLOW_MULTIPLE_SESSIONS NOT IN (0,1)"));
            if (invalidValues != 0)
                throw new InvalidOperationException($"HR_ATTENDANCE_POLICY.ALLOW_MULTIPLE_SESSIONS contains {invalidValues} values outside the supported boolean range; no installer DDL was applied.");
        }
    }

    private static async Task EnsureNullableColumn(OracleConnection connection, string table, string column, string kind, int? length, string definition, List<string> report)
    {
        if (await ValidateNullableColumn(connection, table, column, kind, length))
        {
            report.Add($"{table}.{column}: existing nullable definition matches EF.");
            return;
        }

        await Execute(connection, $"ALTER TABLE DEV_TEMPLATE.\"{table}\" ADD (\"{column}\" {definition})");
        report.Add($"{table}.{column}: added as an EF-compatible nullable column.");
    }

    private static async Task<bool> ValidateNullableColumn(OracleConnection connection, string table, string column, string kind, int? length)
    {
        var metadata = await Query(connection, "SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE,DATA_PRECISION,DATA_SCALE,NVL(DEFAULT_LENGTH,0),VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table AND COLUMN_NAME=:p_column",
            ("p_table", table), ("p_column", column));
        if (metadata.Count > 1) throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} metadata is ambiguous.");
        if (metadata.Count == 0) return false;
        if (metadata.Count == 1)
        {
            var row = metadata[0];
            var valid = row[2] == "Y" && row[5] == "0" && row[6] == "NO" && (kind switch
            {
                "TEXT" => row[0] == "NVARCHAR2" && int.TryParse(row[1], out var actualLength) && length.HasValue && actualLength >= length.Value,
                "NUMBER" => row[0] == "NUMBER" && row[3] == "19" && (row[4] == "0" || string.IsNullOrEmpty(row[4])),
                "TIMESTAMP" => row[0] == "TIMESTAMP(7)",
                _ => false
            });
            if (!valid)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} has an incompatible nullable-column definition.");
            return true;
        }
        return false;
    }

    private static async Task PreflightRequiredMappedColumns(OracleConnection connection)
    {
        foreach (var (table, requiredColumns) in RequiredMappedColumns)
        {
            var tableExists = await Scalar(connection, "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table", ("p_table", table));
            if (Convert.ToInt32(tableExists) == 0) continue;

            var actual = await Query(connection, "SELECT COLUMN_NAME FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table", ("p_table", table));
            var found = actual.Select(row => row[0]).ToHashSet(StringComparer.Ordinal);
            var missing = requiredColumns.Where(column => !found.Contains(column)).ToArray();
            if (missing.Length != 0)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing required mapped columns ({string.Join(", ", missing)}); review and backfill before installer DDL.");
        }
    }

    private static async Task PreflightIndexesAndConstraints(OracleConnection connection)
    {
        var indexes = new (string Name, string Table, string Columns)[]
        {
            ("IX_HR_EOS_POL_COMP", "HR_END_OF_SERVICE_POLICY", "COMPANY_ID,IS_ACTIVE"),
            ("IX_HR_EOS_EMP_STATUS", "HR_END_OF_SERVICE_SETTLEMENT", "COMPANY_ID,EMPLOYEE_CODE,STATUS"),
            ("IX_HR_EOS_LINE_SETTLE", "HR_EOS_SETTLEMENT_LINE", "SETTLEMENT_ID"),
            ("IX_HR_SHORT_LEAVE_POLICY_COMP", "HR_SHORT_LEAVE_POLICY", "COMPANY_ID"),
            ("IX_HR_SHORT_LEAVE_EMP_DATE", "HR_SHORT_LEAVE_REQUEST", "EMPLOYEE_CODE,PERMISSION_DATE"),
            ("IX_HR_SHORT_LEAVE_COMP_STAT", "HR_SHORT_LEAVE_REQUEST", "COMPANY_ID,STATUS")
        };
        foreach (var (name, table, columns) in indexes)
        {
            var rows = await Query(connection, """
                SELECT i.TABLE_NAME,i.UNIQUENESS,i.STATUS,
                       LISTAGG(c.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY c.COLUMN_POSITION)
                FROM ALL_INDEXES i
                JOIN ALL_IND_COLUMNS c ON c.INDEX_OWNER=i.OWNER AND c.INDEX_NAME=i.INDEX_NAME
                WHERE i.OWNER='DEV_TEMPLATE' AND i.INDEX_NAME=:p_name
                GROUP BY i.TABLE_NAME,i.UNIQUENESS,i.STATUS
                """, ("p_name", name));
            if (rows.Count > 1 || rows.Count == 1 &&
                (rows[0][0] != table || rows[0][1] != "NONUNIQUE" || rows[0][2] != "VALID" || rows[0][3] != columns))
                throw new InvalidOperationException($"Existing index {name} conflicts with the EOS/short-leave mapping; no installer DDL was applied.");
        }

        var constraints = new (string Name, string Table, string Column, string RefTable, string RefColumn, string DeleteRule)[]
        {
            ("FK_HR_EOS_EMPLOYEE", "HR_END_OF_SERVICE_SETTLEMENT", "EMPLOYEE_CODE", "HR_EMPLOYEE", "EMPLOYEE_CODE", "NO ACTION"),
            ("FK_HR_SHORT_LEAVE_EMPLOYEE", "HR_SHORT_LEAVE_REQUEST", "EMPLOYEE_CODE", "HR_EMPLOYEE", "EMPLOYEE_CODE", "NO ACTION"),
            ("FK_HR_EOS_LINE_SETTLE", "HR_EOS_SETTLEMENT_LINE", "SETTLEMENT_ID", "HR_END_OF_SERVICE_SETTLEMENT", "ID", "CASCADE")
        };
        foreach (var (name, table, column, refTable, refColumn, deleteRule) in constraints)
        {
            var rows = await Query(connection, """
                SELECT c.TABLE_NAME,c.CONSTRAINT_TYPE,c.STATUS,c.VALIDATED,NVL(c.DELETE_RULE,'-'),
                       LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION),r.TABLE_NAME,
                       LISTAGG(rc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION)
                FROM ALL_CONSTRAINTS c
                JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                LEFT JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME
                LEFT JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME AND rc.POSITION=cc.POSITION
                WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME=:p_name
                GROUP BY c.TABLE_NAME,c.CONSTRAINT_TYPE,c.STATUS,c.VALIDATED,c.DELETE_RULE,r.TABLE_NAME
                """, ("p_name", name));
            if (rows.Count > 1 || rows.Count == 1 &&
                (rows[0][0] != table || rows[0][1] != "R" || rows[0][2] != "ENABLED" || rows[0][3] != "VALIDATED" ||
                 rows[0][4] != deleteRule || rows[0][5] != column || rows[0][6] != refTable || rows[0][7] != refColumn))
                throw new InvalidOperationException($"Existing constraint {name} conflicts with the EOS/short-leave mapping; no installer DDL was applied.");
        }

        var parentKey = await Scalar(connection, """
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_EMPLOYEE' AND c.CONSTRAINT_TYPE IN ('P','U')
              AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
              AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION)
                   FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='EMPLOYEE_CODE'
            """);
        if (Convert.ToInt32(parentKey) == 0)
            throw new InvalidOperationException("HR_EMPLOYEE.EMPLOYEE_CODE lacks an enabled, validated unique/primary key required by EOS and short-leave foreign keys; no installer DDL was applied.");

        if (await TableExists(connection, "HR_END_OF_SERVICE_SETTLEMENT"))
        {
            var settlementKey = await Scalar(connection, """
                SELECT COUNT(*) FROM ALL_CONSTRAINTS c
                WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_END_OF_SERVICE_SETTLEMENT'
                  AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                  AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION)
                       FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='ID'
                """);
            if (Convert.ToInt32(settlementKey) == 0)
                throw new InvalidOperationException("Existing HR_END_OF_SERVICE_SETTLEMENT.ID lacks an enabled, validated primary/unique key required by settlement-line FK; no installer DDL was applied.");
        }
    }

    private static async Task PreflightForeignKeyData(OracleConnection connection)
    {
        var links = new (string Table, string Column, string RefTable, string RefColumn)[]
        {
            ("HR_END_OF_SERVICE_SETTLEMENT", "EMPLOYEE_CODE", "HR_EMPLOYEE", "EMPLOYEE_CODE"),
            ("HR_SHORT_LEAVE_REQUEST", "EMPLOYEE_CODE", "HR_EMPLOYEE", "EMPLOYEE_CODE"),
            ("HR_EOS_SETTLEMENT_LINE", "SETTLEMENT_ID", "HR_END_OF_SERVICE_SETTLEMENT", "ID")
        };
        foreach (var (table, column, refTable, refColumn) in links)
        {
            if (!await TableExists(connection, table)) continue;
            if (!await TableExists(connection, refTable))
            {
                var childRows = await Scalar(connection, $"SELECT COUNT(*) FROM DEV_TEMPLATE.{table} WHERE {column} IS NOT NULL");
                if (Convert.ToInt64(childRows) != 0)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} contains values but referenced table {refTable} is missing; no installer DDL was applied.");
                continue;
            }
            var orphans = await Scalar(connection, $"SELECT COUNT(*) FROM DEV_TEMPLATE.{table} c WHERE c.{column} IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.{refTable} p WHERE p.{refColumn}=c.{column})");
            if (Convert.ToInt64(orphans) != 0)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} contains {orphans} rows without a matching {refTable}.{refColumn}; no installer DDL was applied.");
        }
    }

    private static Task PreflightScalarColumnDefinitions(OracleConnection connection) => ValidateScalarColumnDefinitions(connection, requireTables: false);

    private static async Task ValidateRequiredTextColumnDefinitions(OracleConnection connection, bool requireTables)
    {
        foreach (var table in RequiredMappedColumns.Keys)
        {
            if (!await TableExists(connection, table))
            {
                if (requireTables)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing during required text-column verification.");
                continue;
            }
        }

        foreach (var column in RequiredTextColumns)
        {
            if (!await TableExists(connection, column.Table)) continue;
            var metadata = await Query(connection,
                "SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table AND COLUMN_NAME=:p_column",
                ("p_table", column.Table), ("p_column", column.Column));
            if (metadata.Count != 1 || metadata[0][0] != "NVARCHAR2" ||
                !int.TryParse(metadata[0][1], out var length) || length < column.Length ||
                metadata[0][2] != "N" || metadata[0][3] != "NO")
                throw new InvalidOperationException($"DEV_TEMPLATE.{column.Table}.{column.Column} must be required, non-virtual NVARCHAR2({column.Length}) or wider before EOS/short-leave installer DDL.");
        }
    }

    private static async Task ValidateScalarColumnDefinitions(OracleConnection connection, bool requireTables)
    {
        foreach (var table in RequiredMappedColumns.Keys)
        {
            var tableExists = await Scalar(connection, "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table", ("p_table", table));
            if (Convert.ToInt32(tableExists) == 0)
            {
                if (requireTables) throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing during schema verification.");
                continue;
            }
        }

        foreach (var column in RequiredNumberColumns)
        {
            if (!await TableExists(connection, column.Table)) continue;
            var metadata = await Query(connection, "SELECT DATA_TYPE,DATA_PRECISION,DATA_SCALE,NULLABLE,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table AND COLUMN_NAME=:p_column",
                ("p_table", column.Table), ("p_column", column.Column));
            if (metadata.Count != 1 || metadata[0][0] != "NUMBER" || metadata[0][1] != column.Precision.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                (int.TryParse(metadata[0][2], out var scale) ? scale : 0) != column.Scale || metadata[0][3] != "N" || metadata[0][4] != "NO")
                throw new InvalidOperationException($"DEV_TEMPLATE.{column.Table}.{column.Column} must be NUMBER({column.Precision},{column.Scale}) and non-virtual.");
        }

        foreach (var column in RequiredTimestampColumns)
        {
            if (!await TableExists(connection, column.Table)) continue;
            var metadata = await Query(connection, "SELECT DATA_TYPE,NULLABLE,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table AND COLUMN_NAME=:p_column",
                ("p_table", column.Table), ("p_column", column.Column));
            if (metadata.Count != 1 || metadata[0][0] != "TIMESTAMP(7)" || metadata[0][1] != "N" || metadata[0][2] != "NO")
                throw new InvalidOperationException($"DEV_TEMPLATE.{column.Table}.{column.Column} must be TIMESTAMP(7) and non-virtual.");
        }

        foreach (var column in RequiredIntervalColumns)
        {
            if (!await TableExists(connection, column.Table)) continue;
            var metadata = await Query(connection, "SELECT DATA_TYPE,NULLABLE,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table AND COLUMN_NAME=:p_column",
                ("p_table", column.Table), ("p_column", column.Column));
            if (metadata.Count != 1 || metadata[0][0] != "INTERVAL DAY(8) TO SECOND(7)" || metadata[0][1] != "N" || metadata[0][2] != "NO")
                throw new InvalidOperationException($"DEV_TEMPLATE.{column.Table}.{column.Column} must be INTERVAL DAY(8) TO SECOND(7) and non-virtual.");
        }
    }

    private static async Task<bool> TableExists(OracleConnection connection, string table)
    {
        var exists = await Scalar(connection, "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:p_table", ("p_table", table));
        return Convert.ToInt32(exists) == 1;
    }

    private static async Task EnsureIndex(OracleConnection connection, string name, string ddl, List<string> report)
    {
        var exists = await Scalar(connection, "SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME=:p_name", ("p_name", name));
        if (Convert.ToInt32(exists) == 0)
        {
            await Execute(connection, ddl);
            report.Add($"{name}: created.");
        }
        else report.Add($"{name}: already exists.");
    }

    private static async Task EnsureConstraint(OracleConnection connection, string name, string ddl, List<string> report)
    {
        var exists = await Scalar(connection, "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:p_name", ("p_name", name));
        if (Convert.ToInt32(exists) == 0)
        {
            await Execute(connection, ddl);
            report.Add($"{name}: created.");
        }
        else report.Add($"{name}: already exists.");
    }

    private static async Task<object?> Scalar(OracleConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.Add(name, value);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<List<string[]>> Query(OracleConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.Add(name, value);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? "NULL" : reader.GetValue(index).ToString()!).ToArray());
        return rows;
    }

    private static async Task Execute(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 15;
        await command.ExecuteNonQueryAsync();
    }
}
