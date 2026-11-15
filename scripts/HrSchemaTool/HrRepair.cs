using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;

internal static class HrRepair
{
    private sealed record MappedColumnAddition(
        string Table,
        string Column,
        string Definition,
        string OracleType,
        bool Nullable,
        int? CharacterLength,
        int? DataLength,
        int? Precision,
        int? Scale,
        bool HasDefault);
    private static readonly HashSet<string> MappedRepairTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "HR_ATTENDANCE_DAY", "HR_ATTENDANCE_POLICY", "HR_RAW_ATTENDANCE", "HR_LEAVE_TYPE", "HR_LEAVE_POLICY",
        "HR_LEAVE_BALANCE", "HR_LEAVE_SETTINGS", "HR_PAYROLL_RUN", "HR_PAYROLL_RUN_LINE", "HR_PRORATION_POLICY",
        "HR_LEAVE_YEAR_CLOSE", "HR_LEAVE_YEAR_CLOSE_LINE"
    };

    private static readonly (string Source, string Target, (string SourceColumn, string TargetColumn)[] Columns)[] LegacyTableTransfers =
    [
        ("HR_EMP_SALARY_STRUCTURE", "HR_SALARY_STRUCTURE", [
            ("ID", "ID"), ("EMPLOYEE_CODE", "EMPLOYEE_CODE"), ("EFFECTIVE_FROM", "EFFECTIVE_FROM"),
            ("EFFECTIVE_TO", "EFFECTIVE_TO"), ("BASIC_SALARY", "BASIC_SALARY"), ("CURRENCY_CODE", "CURRENCY_CODE"),
            ("PAYMENT_METHOD", "PAYMENT_METHOD"), ("IS_ACTIVE", "IS_ACTIVE"), ("CREATION_USER", "CREATION_USER"),
            ("CREATION_DATE", "CREATION_DATE"), ("UPDATE_USER", "UPDATE_USER"), ("UPDATE_DATE", "UPDATE_DATE")
        ]),
        ("HR_EMP_SALARY_STRUCT_LINE", "HR_SALARY_STRUCTURE_LINE", [
            ("ID", "ID"), ("STRUCTURE_ID", "STRUCTURE_ID"), ("COMPONENT_CODE", "COMPONENT_CODE"),
            ("AMOUNT", "AMOUNT"), ("PERCENT_VALUE", "PERCENT"), ("IS_ACTIVE", "IS_ACTIVE"),
            ("CREATION_USER", "CREATION_USER"), ("CREATION_DATE", "CREATION_DATE")
        ]),
        ("HR_EMP_SHIFT_ASSIGNMENT", "HR_EMPLOYEE_SHIFT_ASSIGNMENT", [
            ("ID", "ID"), ("EMPLOYEE_CODE", "EMPLOYEE_CODE"), ("SHIFT_CODE", "SHIFT_CODE"),
            ("EFFECTIVE_FROM", "EFFECTIVE_FROM"), ("EFFECTIVE_TO", "EFFECTIVE_TO"), ("IS_ACTIVE", "IS_ACTIVE"),
            ("CREATION_USER", "CREATION_USER"), ("CREATION_DATE", "CREATION_DATE"),
            ("UPDATE_USER", "UPDATE_USER"), ("UPDATE_DATE", "UPDATE_DATE")
        ])
    ];

    public static async Task Apply(OracleConnection connection, List<string> report)
    {
        async Task Run(string sql) { await using var c = connection.CreateCommand(); c.CommandText = sql; c.CommandTimeout = 30; await c.ExecuteNonQueryAsync(); }
        async Task<long> Count(string sql) { await using var c = connection.CreateCommand(); c.CommandText = sql; return Convert.ToInt64(await c.ExecuteScalarAsync()); }
        await Run("ALTER SESSION SET CURRENT_SCHEMA = DEV_TEMPLATE");
        await PreflightBootstrapObjects(connection);
        List<MappedColumnAddition> mappedColumnAdditions;
        using (var preflightDb = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options))
        {
            await HrLeavePolicySchema.PreflightAsync(connection);
            await PreflightMappedHrTables(connection, preflightDb);
            await HrStringRepair.PreflightAsync(connection, preflightDb);
            mappedColumnAdditions = await PlanMappedColumnAdditionsAsync(connection, preflightDb);
        }
        await PreflightLeaveBalanceCycleMigration(connection);
        await PreflightLeaveBalancePrecision(connection);
        await PreflightLegacyTableTransfers(connection);
        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_GL_POSTING_CONFIG'") == 0)
        {
            await Run("CREATE TABLE DEV_TEMPLATE.HR_GL_POSTING_CONFIG (BRANCH_ID NUMBER(19) PRIMARY KEY, SALARYEXPENSE NVARCHAR2(100) NOT NULL, EMPLOYERSSCEXPENSE NVARCHAR2(100) NOT NULL, SALARYPAYABLE NVARCHAR2(100) NOT NULL, SSCPAYABLE NVARCHAR2(100) NOT NULL, TAXPAYABLE NVARCHAR2(100) NOT NULL, DEDUCTIONSPAYABLE NVARCHAR2(100) NOT NULL, LOAN_RECEIVABLE_ACCOUNT NVARCHAR2(100) NULL, ADVANCE_RECEIVABLE_ACCOUNT NVARCHAR2(100) NULL, CURRENCY_ID NUMBER(19) NOT NULL, VOUCHER_TYPE NUMBER(10) NOT NULL, CONSTRAINT FK_HR_GL_CONFIG_BRANCH FOREIGN KEY (BRANCH_ID) REFERENCES DEV_TEMPLATE.SYS_BRANCH (\"Id\"))");
            report.Add("Created HR_GL_POSTING_CONFIG. Posting accounts require explicit configuration; no account choices guessed.");
        }
        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_SETTINGS'") == 0)
        {
            await Run("CREATE TABLE DEV_TEMPLATE.HR_LEAVE_SETTINGS (ID NUMBER(19) PRIMARY KEY, LEAVE_YEAR_START_MONTH NUMBER(10) NOT NULL, LEAVE_YEAR_START_DAY NUMBER(10) NOT NULL, BALANCE_DISPLAY_FORMAT NVARCHAR2(20) NOT NULL, STANDARD_WORKDAY_HOURS NUMBER(6,2) NULL, CREATION_USER NVARCHAR2(200) NOT NULL, CREATION_DATE TIMESTAMP(7) NOT NULL, UPDATE_USER NVARCHAR2(200) NULL, UPDATE_DATE TIMESTAMP(7) NULL)");
            report.Add("Created HR_LEAVE_SETTINGS; tenant default values are applied by the API on first save.");
        }
        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_YEAR_CLOSE'") == 0)
        {
            await Run("CREATE TABLE DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, CLOSE_KEY NVARCHAR2(300) NOT NULL UNIQUE, FROM_YEAR NUMBER(10) NOT NULL, TO_YEAR NUMBER(10) NOT NULL, LEAVE_TYPE_CODE NVARCHAR2(100) NOT NULL, STATUS NVARCHAR2(20) NOT NULL, CREATION_USER NVARCHAR2(200) NOT NULL, CREATION_DATE TIMESTAMP(7) NOT NULL, POSTED_BY NVARCHAR2(200) NULL, POSTED_DATE TIMESTAMP(7) NULL, UNPOSTED_BY NVARCHAR2(200) NULL, UNPOSTED_DATE TIMESTAMP(7) NULL)");
            report.Add("Created HR_LEAVE_YEAR_CLOSE for draft/post/unpost close state.");
        }
        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_YEAR_CLOSE_LINE'") == 0)
        {
            await Run("CREATE TABLE DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, CLOSE_ID NUMBER(19) NOT NULL, EMPLOYEE_CODE NVARCHAR2(100) NOT NULL, LEAVE_TYPE_CODE NVARCHAR2(100) NOT NULL, SOURCE_YEAR NUMBER(10) NOT NULL, TARGET_YEAR NUMBER(10) NOT NULL, SOURCE_BALANCE_ID NUMBER(19) NOT NULL, SOURCE_CYCLE_START_DATE TIMESTAMP(7) NULL, SOURCE_CYCLE_END_DATE TIMESTAMP(7) NULL, TARGET_CYCLE_START_DATE TIMESTAMP(7) NOT NULL, TARGET_CYCLE_END_DATE TIMESTAMP(7) NOT NULL, SOURCE_ACCRUED_BEFORE NUMBER(8,4) NOT NULL, SOURCE_USED_DAYS NUMBER(8,4) NOT NULL, SOURCE_CARRIED_BEFORE NUMBER(8,4) NOT NULL, TARGET_ACCRUED_BEFORE NUMBER(8,4) NOT NULL, TARGET_CARRIED_BEFORE NUMBER(8,4) NOT NULL, TARGET_USED_BEFORE NUMBER(8,4) NOT NULL, UNUSED_DAYS NUMBER(8,4) NOT NULL, CARRIED_DAYS NUMBER(8,4) NOT NULL, EXPIRED_DAYS NUMBER(8,4) NOT NULL, CASHOUT_DAYS NUMBER(8,4) NOT NULL, TARGET_BALANCE_ID NUMBER(19) NULL, CONSTRAINT FK_HR_LEAVE_CLOSE_LINE_HDR FOREIGN KEY (CLOSE_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE(ID) ON DELETE CASCADE, CONSTRAINT UX_HR_LEAVE_CLOSE_LINE UNIQUE (CLOSE_ID, SOURCE_BALANCE_ID))");
            report.Add("Created HR_LEAVE_YEAR_CLOSE_LINE with immutable source/target balance snapshots.");
        }
        if (await Count("SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE' AND COLUMN_NAME IN ('ACCRUED_DAYS','USED_DAYS','CARRIED_FORWARD_DAYS')") == 3)
        {
            await Run("ALTER TABLE DEV_TEMPLATE.HR_LEAVE_BALANCE MODIFY (ACCRUED_DAYS NUMBER(8,4), USED_DAYS NUMBER(8,4), CARRIED_FORWARD_DAYS NUMBER(8,4))");
            report.Add("Expanded HR_LEAVE_BALANCE day precision to four decimal places for daily accrual.");
        }
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await HrStringRepair.RepairAsync(connection, db, report);
        await HrLeavePolicySchema.Install(connection, report);
        foreach (var addition in mappedColumnAdditions)
        {
            await Run($"ALTER TABLE DEV_TEMPLATE.{QuoteIdentifier(addition.Table)} ADD ({QuoteIdentifier(addition.Column)} {addition.Definition})");
            report.Add($"Added {addition.Table}.{addition.Column}: {addition.Definition}");
        }
        await VerifyMappedColumnAdditionsAsync(connection, mappedColumnAdditions);
        // Historical balances used calendar years. Preserve that meaning rather than
        // interpreting old balances with a company's newly configured cycle.
        if (await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE (CYCLE_START_DATE IS NULL OR CYCLE_END_DATE IS NULL) AND (YEAR_NO IS NULL OR YEAR_NO<1 OR YEAR_NO>9998 OR YEAR_NO<>TRUNC(YEAR_NO))") > 0)
            throw new InvalidOperationException("Legacy balance years require manual review before cycle migration.");
        await using (var transaction = connection.BeginTransaction())
        {
            await Run("UPDATE DEV_TEMPLATE.HR_LEAVE_BALANCE SET CYCLE_START_DATE=TO_DATE(TO_CHAR(YEAR_NO,'FM0000')||'0101','YYYYMMDD'), CYCLE_END_DATE=TO_DATE(TO_CHAR(YEAR_NO,'FM0000')||'1231','YYYYMMDD') WHERE CYCLE_START_DATE IS NULL AND CYCLE_END_DATE IS NULL");
            if (await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE CYCLE_START_DATE IS NULL OR CYCLE_END_DATE IS NULL OR CYCLE_END_DATE<CYCLE_START_DATE") > 0 ||
                await Count("SELECT COUNT(*) FROM (SELECT EMPLOYEE_CODE,LEAVE_TYPE_CODE,CYCLE_START_DATE FROM DEV_TEMPLATE.HR_LEAVE_BALANCE GROUP BY EMPLOYEE_CODE,LEAVE_TYPE_CODE,CYCLE_START_DATE HAVING COUNT(*)>1)") > 0)
                throw new InvalidOperationException("Incomplete, invalid or duplicate balance cycles require manual review.");
            await transaction.CommitAsync();
        }
        // Establish the replacement before dropping any existing uniqueness protection.
        if (await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS c WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_LEAVE_BALANCE' AND c.CONSTRAINT_TYPE='U' AND (SELECT LISTAGG(x.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY x.COLUMN_NAME) FROM ALL_CONS_COLUMNS x WHERE x.OWNER=c.OWNER AND x.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='CYCLE_START_DATE,EMPLOYEE_CODE,LEAVE_TYPE_CODE'") == 0)
            await Run("ALTER TABLE DEV_TEMPLATE.HR_LEAVE_BALANCE ADD CONSTRAINT UX_HR_LEAVE_BALANCE_CYCLE UNIQUE (EMPLOYEE_CODE,LEAVE_TYPE_CODE,CYCLE_START_DATE)");
        var legacyKeys = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT c.CONSTRAINT_NAME FROM ALL_CONSTRAINTS c WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_LEAVE_BALANCE' AND c.CONSTRAINT_TYPE='U' AND (SELECT LISTAGG(x.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY x.COLUMN_NAME) FROM ALL_CONS_COLUMNS x WHERE x.OWNER=c.OWNER AND x.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='EMPLOYEE_CODE,LEAVE_TYPE_CODE,YEAR_NO'";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) legacyKeys.Add(reader.GetString(0));
        }
        foreach (var key in legacyKeys)
            await Run($"ALTER TABLE DEV_TEMPLATE.HR_LEAVE_BALANCE DROP CONSTRAINT \"{key.Replace("\"", "\"\"")}\"");
        // Standalone unique indexes can also enforce the obsolete year key.
        legacyKeys.Clear();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT i.INDEX_NAME FROM ALL_INDEXES i WHERE i.OWNER='DEV_TEMPLATE' AND i.TABLE_NAME='HR_LEAVE_BALANCE' AND i.UNIQUENESS='UNIQUE' AND (SELECT LISTAGG(x.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY x.COLUMN_NAME) FROM ALL_IND_COLUMNS x WHERE x.INDEX_OWNER=i.OWNER AND x.INDEX_NAME=i.INDEX_NAME)='EMPLOYEE_CODE,LEAVE_TYPE_CODE,YEAR_NO'";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) legacyKeys.Add(reader.GetString(0));
        }
        foreach (var key in legacyKeys)
            await Run($"DROP INDEX DEV_TEMPLATE.\"{key.Replace("\"", "\"\"")}\"");
        report.Add("Legacy calendar-year balances dated; cycle uniqueness established before removing obsolete year keys by column signature.");
        foreach (var table in new[] { "HR_SALARY_STRUCTURE", "HR_SALARY_STRUCTURE_LINE", "HR_EMPLOYEE_SHIFT_ASSIGNMENT" })
            if (await Count($"SELECT COUNT(*) FROM ALL_TAB_IDENTITY_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='ID'") > 0)
                await Run($"ALTER TABLE DEV_TEMPLATE.{table} MODIFY (ID GENERATED BY DEFAULT ON NULL AS IDENTITY)");
        await using (var transaction = connection.BeginTransaction())
        {
            foreach (var map in LegacyTableTransfers)
            {
                var before = await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.{map.Target}");
                var targetColumns = string.Join(',', map.Columns.Select(c => c.TargetColumn));
                var sourceColumns = string.Join(',', map.Columns.Select(c => "s." + c.SourceColumn));
                // Retain original tables and stable IDs; repeat runs insert only missing IDs.
                await Run($"INSERT INTO DEV_TEMPLATE.{map.Target} ({targetColumns}) SELECT {sourceColumns} FROM DEV_TEMPLATE.{map.Source} s WHERE NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.{map.Target} t WHERE t.ID=s.ID)");
                var after = await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.{map.Target}");
                report.Add($"Migrated {map.Source} -> {map.Target}: added {after-before}, total {after}; original retained.");
            }
            await transaction.CommitAsync();
        }
        foreach (var table in new[] { "HR_SALARY_STRUCTURE", "HR_SALARY_STRUCTURE_LINE", "HR_EMPLOYEE_SHIFT_ASSIGNMENT" })
            if (await Count($"SELECT COUNT(*) FROM ALL_TAB_IDENTITY_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='ID'") > 0)
                await Run($"ALTER TABLE DEV_TEMPLATE.{table} MODIFY (ID GENERATED BY DEFAULT ON NULL AS IDENTITY (START WITH LIMIT VALUE))");

        var hrScreen = await db.Set<SysScreen>().FirstOrDefaultAsync(s => s.ScreenCode == "hr-employees");
        var hr = await db.Set<SysSystem>().FirstOrDefaultAsync(s => s.Id == hrScreen!.SystemId)
            ?? throw new InvalidOperationException("HR system registration is missing.");
        var featureCodes = new[] { "view", "create", "edit", "delete", "approve", "reject", "export", "import", "post", "reverse", "link-user", "configure" };
        foreach (var code in featureCodes)
            if (!await db.Set<SysFeature>().AnyAsync(f => f.FeatureCode == code))
                db.Add(new SysFeature { FeatureCode = code, FeatureName = code, FeatureNameE = code, IsActive = true, CreationUser = "HR_REPAIR", CreationDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        foreach (var code in new[] { "employees", "attendance", "attendance-corrections", "leaves", "loans", "payroll", "payroll-adjustments", "payroll-policies", "salary-components", "work-calendars", "payroll-posting" })
        {
            var screen = await db.Set<SysScreen>().FirstOrDefaultAsync(s => s.ScreenCode == "hr-" + code);
            if (screen == null)
            {
                screen = new SysScreen { ScreenCode = "hr-"+code, ScreenName = code, ScreenNameE = code, SystemId = hr.Id, IsActive = true, CreationUser = "HR_REPAIR", CreationDate = DateTime.UtcNow };
                db.Add(screen); await db.SaveChangesAsync();
            }
            foreach (var feature in await db.Set<SysFeature>().Where(f => featureCodes.Contains(f.FeatureCode)).ToListAsync())
                if (!await db.Set<SysScreenFeature>().AnyAsync(sf => sf.ScreenId == screen.Id && sf.FeatureId == feature.Id))
                    db.Add(new SysScreenFeature { ScreenId = screen.Id, FeatureId = feature.Id });
            await db.SaveChangesAsync();
        }
        report.Add("HR screens and explicit operation features registered. No role/user grants were added.");
    }

    private static async Task PreflightBootstrapObjects(OracleConnection connection)
    {
        async Task<long> Count(string sql, params OracleParameter[] parameters)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters) command.Parameters.Add(parameter);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        foreach (var table in new[] { "HR_GL_POSTING_CONFIG", "HR_LEAVE_SETTINGS", "HR_LEAVE_YEAR_CLOSE", "HR_LEAVE_YEAR_CLOSE_LINE" })
        {
            var tableExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName",
                new OracleParameter("tableName", table)) != 0;
            if (tableExists) continue;
            var objectExists = await Count("SELECT COUNT(*) FROM ALL_OBJECTS WHERE OWNER='DEV_TEMPLATE' AND OBJECT_NAME=:objectName",
                new OracleParameter("objectName", table)) != 0;
            if (objectExists)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is occupied by a non-table object; HR repair stopped before schema DDL.");
        }

        var expectedConstraintNames = new[]
        {
            (Table: "HR_GL_POSTING_CONFIG", Name: "FK_HR_GL_CONFIG_BRANCH"),
            (Table: "HR_LEAVE_YEAR_CLOSE_LINE", Name: "FK_HR_LEAVE_CLOSE_LINE_HDR"),
            (Table: "HR_LEAVE_YEAR_CLOSE_LINE", Name: "UX_HR_LEAVE_CLOSE_LINE")
        };
        foreach (var (table, name) in expectedConstraintNames)
        {
            var tableExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName",
                new OracleParameter("tableName", table)) != 0;
            if (tableExists) continue;
            if (await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName",
                    new OracleParameter("constraintName", name)) != 0)
                throw new InvalidOperationException($"DEV_TEMPLATE.{name} is reserved by another table; HR repair stopped before schema DDL.");
        }

        var postingConfigExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_GL_POSTING_CONFIG'") != 0;
        if (!postingConfigExists && await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='SYS_BRANCH'") == 0)
            throw new InvalidOperationException("DEV_TEMPLATE.SYS_BRANCH is missing; HR repair stopped before schema DDL.");
        if (!postingConfigExists && await Count("""
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='SYS_BRANCH'
              AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
              AND c.CONSTRAINT_NAME IN (
                  SELECT cc.CONSTRAINT_NAME FROM ALL_CONS_COLUMNS cc
                  WHERE cc.OWNER='DEV_TEMPLATE' AND cc.TABLE_NAME='SYS_BRANCH'
                  GROUP BY cc.CONSTRAINT_NAME
                  HAVING COUNT(*)=1 AND MIN(cc.COLUMN_NAME)='Id')
            """) == 0)
            throw new InvalidOperationException("DEV_TEMPLATE.SYS_BRANCH.\"Id\" has no enabled, validated single-column primary/unique key; HR repair stopped before schema DDL.");

        var closeLineExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_YEAR_CLOSE_LINE'") != 0;
        var closeHeaderExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_YEAR_CLOSE'") != 0;
        if (closeLineExists && !closeHeaderExists)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE exists without HR_LEAVE_YEAR_CLOSE; HR repair stopped before schema DDL.");
        if (!closeLineExists && closeHeaderExists && await Count("""
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_LEAVE_YEAR_CLOSE'
              AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
              AND c.CONSTRAINT_NAME IN (
                  SELECT cc.CONSTRAINT_NAME FROM ALL_CONS_COLUMNS cc
                  WHERE cc.OWNER='DEV_TEMPLATE' AND cc.TABLE_NAME='HR_LEAVE_YEAR_CLOSE'
                  GROUP BY cc.CONSTRAINT_NAME
                  HAVING COUNT(*)=1 AND MIN(cc.COLUMN_NAME)='ID')
            """) == 0)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE.ID has no enabled, validated single-column primary/unique key; HR repair stopped before schema DDL.");
    }

    private static async Task PreflightMappedHrTables(OracleConnection connection, OracleDbContext db)
    {
        var bootstrapTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HR_GL_POSTING_CONFIG", "HR_LEAVE_SETTINGS", "HR_LEAVE_YEAR_CLOSE", "HR_LEAVE_YEAR_CLOSE_LINE", "HR_LEAVE_POLICY"
        };
        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null && MappedRepairTables.Contains(table))
            .Select(table => table!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(table => !bootstrapTables.Contains(table));

        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
            command.Parameters.Add(new OracleParameter("tableName", table));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; HR repair stopped before schema DDL because it cannot add mapped columns to a missing table.");
        }
    }

    private static async Task<List<MappedColumnAddition>> PlanMappedColumnAdditionsAsync(OracleConnection connection, OracleDbContext db)
    {
        var bootstrapColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["HR_GL_POSTING_CONFIG"] = new(["BRANCH_ID", "SALARYEXPENSE", "EMPLOYERSSCEXPENSE", "SALARYPAYABLE", "SSCPAYABLE", "TAXPAYABLE", "DEDUCTIONSPAYABLE", "LOAN_RECEIVABLE_ACCOUNT", "ADVANCE_RECEIVABLE_ACCOUNT", "CURRENCY_ID", "VOUCHER_TYPE"], StringComparer.OrdinalIgnoreCase),
            ["HR_LEAVE_SETTINGS"] = new(["ID", "LEAVE_YEAR_START_MONTH", "LEAVE_YEAR_START_DAY", "BALANCE_DISPLAY_FORMAT", "STANDARD_WORKDAY_HOURS", "CREATION_USER", "CREATION_DATE", "UPDATE_USER", "UPDATE_DATE"], StringComparer.OrdinalIgnoreCase),
            ["HR_LEAVE_YEAR_CLOSE"] = new(["ID", "CLOSE_KEY", "FROM_YEAR", "TO_YEAR", "LEAVE_TYPE_CODE", "STATUS", "CREATION_USER", "CREATION_DATE", "POSTED_BY", "POSTED_DATE", "UNPOSTED_BY", "UNPOSTED_DATE"], StringComparer.OrdinalIgnoreCase),
            ["HR_LEAVE_YEAR_CLOSE_LINE"] = new(["ID", "CLOSE_ID", "EMPLOYEE_CODE", "LEAVE_TYPE_CODE", "SOURCE_YEAR", "TARGET_YEAR", "SOURCE_BALANCE_ID", "SOURCE_CYCLE_START_DATE", "SOURCE_CYCLE_END_DATE", "TARGET_CYCLE_START_DATE", "TARGET_CYCLE_END_DATE", "SOURCE_ACCRUED_BEFORE", "SOURCE_USED_DAYS", "SOURCE_CARRIED_BEFORE", "TARGET_ACCRUED_BEFORE", "TARGET_CARRIED_BEFORE", "TARGET_USED_BEFORE", "UNUSED_DAYS", "CARRIED_DAYS", "EXPIRED_DAYS", "CASHOUT_DAYS", "TARGET_BALANCE_ID"], StringComparer.OrdinalIgnoreCase),
            ["HR_LEAVE_POLICY"] = new(["ID", "POLICY_NAME", "LEAVE_TYPE_CODE", "EFFECTIVE_FROM", "EFFECTIVE_TO", "ACCRUAL_METHOD", "ACCRUAL_RATE", "ANNUAL_ENTITLEMENT_DAYS", "CYCLE_BASIS", "PRORATE_FIRST_CYCLE", "APPLICABLE_TO", "MIN_SERVICE_MONTHS", "TIER1_YEARS_THRESHOLD", "TIER1_DAYS", "TIER2_DAYS", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE", "UPDATE_USER", "UPDATE_DATE"], StringComparer.OrdinalIgnoreCase)
        };
        var columnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var tablesToCreate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var defaults = new Dictionary<Type, object?>();
        var additions = new List<MappedColumnAddition>();
        var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null || !MappedRepairTables.Contains(table)) continue;
            if (!columnsByTable.TryGetValue(table, out var existingColumns))
            {
                existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COLUMN_NAME FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
                command.Parameters.Add(new OracleParameter("tableName", table));
                await using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) existingColumns.Add(reader.GetString(0));
                if (existingColumns.Count == 0 && bootstrapColumns.TryGetValue(table, out var createdColumns))
                {
                    tablesToCreate.Add(table);
                    existingColumns.UnionWith(createdColumns);
                }
                columnsByTable.Add(table, existingColumns);
            }

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (string.IsNullOrWhiteSpace(column) || existingColumns.Contains(column)) continue;
                if (table.Equals("HR_LEAVE_POLICY", StringComparison.OrdinalIgnoreCase) &&
                    (column is "EFFECTIVE_FROM" or "EFFECTIVE_TO")) continue; // HrLeavePolicySchema owns the safe effective-date backfill.
                if (property.ClrType == typeof(string) && property.GetMaxLength() is > 0) continue; // HrStringRepair owns bounded strings.
                var key = table + "." + column;
                if (!planned.Add(key)) continue;

                var storeType = property.GetColumnType();
                if (string.IsNullOrWhiteSpace(storeType) || storeType.Any(char.IsControl) || storeType.Contains(';'))
                    throw new InvalidOperationException($"Cannot safely plan {key}: mapped Oracle store type is absent or malformed; HR repair stopped before DDL.");

                string definition;
                if (property.IsNullable)
                {
                    definition = storeType + " NULL";
                }
                else
                {
                    object? value = null;
                    if (!defaults.TryGetValue(entity.ClrType, out var instance))
                    {
                        try { instance = Activator.CreateInstance(entity.ClrType); }
                        catch { instance = null; }
                        defaults.Add(entity.ClrType, instance);
                    }
                    var propertyInfo = property.PropertyInfo ?? entity.ClrType.GetProperty(property.Name);
                    if (instance is not null && propertyInfo is not null) value = propertyInfo.GetValue(instance);

                    var literal = value is null ? null : FormatSqlLiteral(value, storeType);
                    if (string.IsNullOrEmpty(literal))
                    {
                        if (!rowCounts.TryGetValue(table, out var rows))
                        {
                            if (tablesToCreate.Contains(table)) rows = 0;
                            else
                            {
                                await using var count = connection.CreateCommand();
                                count.CommandText = $"SELECT COUNT(*) FROM DEV_TEMPLATE.{QuoteIdentifier(table)}";
                                count.CommandTimeout = 30;
                                rows = Convert.ToInt64(await count.ExecuteScalarAsync());
                            }
                            rowCounts.Add(table, rows);
                        }
                        if (rows > 0)
                            throw new InvalidOperationException($"Required mapped column {key} is missing from a populated table and has no safe literal default; review a backfill before DDL.");
                        definition = storeType + " NOT NULL";
                    }
                    else definition = $"{storeType} DEFAULT {literal} NOT NULL";
                }

                var typeMatch = System.Text.RegularExpressions.Regex.Match(storeType.Trim(), @"^(?<type>[A-Za-z][A-Za-z0-9_]*)(?:\((?<precision>\d+)(?:\s*,\s*(?<scale>\d+))?\))?$");
                if (!typeMatch.Success)
                    throw new InvalidOperationException($"Cannot safely verify {key}: mapped Oracle store type '{storeType}' is not a supported scalar type; HR repair stopped before DDL.");
                var oracleType = typeMatch.Groups["type"].Value.ToUpperInvariant();
                var parsedSize = typeMatch.Groups["precision"].Success
                    ? int.Parse(typeMatch.Groups["precision"].Value, CultureInfo.InvariantCulture)
                    : (int?)null;
                var isCharacterType = oracleType is "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR";
                var isRawType = oracleType == "RAW";
                var characterLength = isCharacterType
                    ? property.GetMaxLength() ?? parsedSize
                    : null;
                var dataLength = isRawType ? parsedSize : null;
                int? precision = !isCharacterType && !isRawType
                    ? parsedSize ?? property.GetPrecision()
                    : null;
                int? scale = !isCharacterType && !isRawType && typeMatch.Groups["scale"].Success
                    ? int.Parse(typeMatch.Groups["scale"].Value, CultureInfo.InvariantCulture)
                    : !isCharacterType && !isRawType ? property.GetScale() : null;
                if (oracleType == "TIMESTAMP" && precision.HasValue)
                {
                    oracleType = $"TIMESTAMP({precision.Value.ToString(CultureInfo.InvariantCulture)})";
                    precision = null;
                    scale = null;
                }
                additions.Add(new MappedColumnAddition(table, column, definition, oracleType, property.IsNullable,
                    characterLength, dataLength, precision, scale, definition.Contains(" DEFAULT ", StringComparison.OrdinalIgnoreCase)));
                existingColumns.Add(column);
            }
        }

        return additions;
    }

    private static async Task VerifyMappedColumnAdditionsAsync(OracleConnection connection, IReadOnlyCollection<MappedColumnAddition> additions)
    {
        foreach (var addition in additions)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,DATA_LENGTH,DATA_PRECISION,DATA_SCALE,NULLABLE,NVL(DEFAULT_LENGTH,0),VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName";
            command.Parameters.Add(new OracleParameter("tableName", addition.Table));
            command.Parameters.Add(new OracleParameter("columnName", addition.Column));
            command.CommandTimeout = 15;
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"DEV_TEMPLATE.{addition.Table}.{addition.Column} was not visible after repair DDL; HR repair stopped before data migration.");

            var type = reader.GetString(0).ToUpperInvariant();
            var charLength = reader.IsDBNull(1) ? (int?)null : Convert.ToInt32(reader.GetValue(1));
            var dataLength = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader.GetValue(2));
            var precision = reader.IsDBNull(3) ? (int?)null : Convert.ToInt32(reader.GetValue(3));
            var scale = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4));
            var nullable = reader.GetString(5).ToUpperInvariant();
            var hasDefault = Convert.ToInt32(reader.GetValue(6)) > 0;
            var isVirtual = !string.Equals(reader.GetString(7), "NO", StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(type, addition.OracleType, StringComparison.OrdinalIgnoreCase) ||
                addition.CharacterLength.HasValue && (!charLength.HasValue || charLength.Value < addition.CharacterLength.Value) ||
                addition.DataLength.HasValue && (!dataLength.HasValue || dataLength.Value < addition.DataLength.Value) ||
                addition.Precision.HasValue && precision != addition.Precision ||
                addition.Scale.HasValue && scale != addition.Scale ||
                !string.Equals(nullable, addition.Nullable ? "Y" : "N", StringComparison.OrdinalIgnoreCase) ||
                hasDefault != addition.HasDefault || isVirtual)
                throw new InvalidOperationException($"DEV_TEMPLATE.{addition.Table}.{addition.Column} does not match its mapped definition after repair DDL; HR repair stopped before data migration.");
        }
    }

    private static string? FormatSqlLiteral(object value, string storeType)
    {
        if (value is string text)
        {
            if (text.Length == 0) return null; // Oracle stores an empty string as NULL.
            var nationalType = storeType.TrimStart();
            var prefix = nationalType.StartsWith("NVARCHAR2", StringComparison.OrdinalIgnoreCase) ||
                nationalType.StartsWith("NCHAR", StringComparison.OrdinalIgnoreCase) ||
                nationalType.StartsWith("NCLOB", StringComparison.OrdinalIgnoreCase) ? "N" : string.Empty;
            return prefix + "'" + text.Replace("'", "''") + "'";
        }
        if (value is bool boolean) return boolean ? "1" : "0";
        if (value is DateTime dateTime)
        {
            if (storeType.StartsWith("TIMESTAMP", StringComparison.OrdinalIgnoreCase))
                return $"TIMESTAMP '{dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}'";
            if (storeType.Equals("DATE", StringComparison.OrdinalIgnoreCase))
                return $"TO_DATE('{dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}','YYYY-MM-DD HH24:MI:SS')";
            return null;
        }
        if (value is DateOnly dateOnly && storeType.Equals("DATE", StringComparison.OrdinalIgnoreCase))
            return $"DATE '{dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'";
        if (value is Guid guid && storeType.StartsWith("RAW", StringComparison.OrdinalIgnoreCase))
            return $"HEXTORAW('{guid.ToString("N", CultureInfo.InvariantCulture)}')";
        if (value.GetType().IsEnum)
            value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal or float or double)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        return null;
    }

    private static string QuoteIdentifier(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static async Task PreflightLeaveBalanceCycleMigration(OracleConnection connection)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE'") != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_BALANCE is missing; repair stopped before DDL.");

        var requiredColumns = await Count("SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE' AND COLUMN_NAME IN ('EMPLOYEE_CODE','LEAVE_TYPE_CODE','YEAR_NO') AND NULLABLE='N'");
        if (requiredColumns != 3)
            throw new InvalidOperationException("HR_LEAVE_BALANCE lacks required EMPLOYEE_CODE, LEAVE_TYPE_CODE or YEAR_NO metadata; repair stopped before DDL.");

        var hasStart = await Count("SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE' AND COLUMN_NAME='CYCLE_START_DATE'") == 1;
        var hasEnd = await Count("SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE' AND COLUMN_NAME='CYCLE_END_DATE'") == 1;
        var start = hasStart ? "CYCLE_START_DATE" : null;
        var end = hasEnd ? "CYCLE_END_DATE" : null;

        foreach (var column in new[] { start, end }.Where(column => column is not null))
        {
            await using var metadata = connection.CreateCommand();
            metadata.CommandText = "SELECT DATA_TYPE,NULLABLE,NVL(DEFAULT_LENGTH,0),VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE' AND COLUMN_NAME=:columnName";
            metadata.Parameters.Add(new OracleParameter("columnName", column));
            await using var reader = await metadata.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(1) != "Y" || Convert.ToInt32(reader.GetValue(2)) != 0 || reader.GetString(3) != "NO" ||
                reader.GetString(0) is not ("DATE" or "TIMESTAMP(7)"))
                throw new InvalidOperationException($"HR_LEAVE_BALANCE.{column} has incompatible cycle-date metadata; repair stopped before DDL.");
        }

        if (hasStart != hasEnd)
        {
            var existingCycleColumn = hasStart ? start! : end!;
            if (await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE {existingCycleColumn} IS NOT NULL") != 0)
                throw new InvalidOperationException("HR_LEAVE_BALANCE has only one cycle boundary and it contains data; repair stopped for manual cycle reconciliation before DDL.");
        }

        if (hasStart && hasEnd && await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE (CYCLE_START_DATE IS NULL AND CYCLE_END_DATE IS NOT NULL) OR (CYCLE_START_DATE IS NOT NULL AND CYCLE_END_DATE IS NULL) OR (CYCLE_START_DATE IS NOT NULL AND CYCLE_END_DATE<CYCLE_START_DATE)") != 0)
            throw new InvalidOperationException("HR_LEAVE_BALANCE contains incomplete or reversed cycle boundaries; repair stopped before DDL.");

        var needsBackfill = hasStart && hasEnd
            ? "CYCLE_START_DATE IS NULL AND CYCLE_END_DATE IS NULL"
            : "1=1";
        if (await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE ({needsBackfill}) AND (YEAR_NO IS NULL OR YEAR_NO<1 OR YEAR_NO>9998 OR YEAR_NO<>TRUNC(YEAR_NO))") != 0)
            throw new InvalidOperationException("Legacy balance years require manual review before cycle repair; no DDL was applied.");

        var effectiveStart = hasStart
            ? "NVL(CYCLE_START_DATE,TO_DATE(TO_CHAR(YEAR_NO,'FM0000')||'0101','YYYYMMDD'))"
            : "TO_DATE(TO_CHAR(YEAR_NO,'FM0000')||'0101','YYYYMMDD')";
        var duplicateCycles = await Count($"SELECT COUNT(*) FROM (SELECT EMPLOYEE_CODE,LEAVE_TYPE_CODE,{effectiveStart} EFFECTIVE_START FROM DEV_TEMPLATE.HR_LEAVE_BALANCE GROUP BY EMPLOYEE_CODE,LEAVE_TYPE_CODE,{effectiveStart} HAVING COUNT(*)>1)");
        if (duplicateCycles != 0)
            throw new InvalidOperationException("HR_LEAVE_BALANCE would contain duplicate employee/type/cycle keys after legacy dating; repair stopped before DDL.");
    }

    private static async Task PreflightLeaveBalancePrecision(OracleConnection connection)
    {
        var columns = new[] { "ACCRUED_DAYS", "USED_DAYS", "CARRIED_FORWARD_DAYS" };
        foreach (var column in columns)
        {
            string? dataType = null;
            int? precision = null;
            var scale = 0;
            string? nullable = null;
            string? virtualColumn = null;
            await using (var metadata = connection.CreateCommand())
            {
                metadata.CommandText = "SELECT DATA_TYPE,DATA_PRECISION,DATA_SCALE,NULLABLE,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE' AND COLUMN_NAME=:columnName";
                metadata.Parameters.Add(new OracleParameter("columnName", column));
                await using var reader = await metadata.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) continue; // HrRepair adds a missing mapped column using its NUMBER(8,4) model type.
                dataType = reader.GetString(0);
                precision = reader.IsDBNull(1) ? null : Convert.ToInt32(reader.GetValue(1));
                scale = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                nullable = reader.GetString(3);
                virtualColumn = reader.GetString(4);
                if (await reader.ReadAsync())
                    throw new InvalidOperationException($"HR_LEAVE_BALANCE.{column} has ambiguous metadata; repair stopped before DDL.");
            }
            if (dataType != "NUMBER" || nullable != "N" || virtualColumn != "NO")
                throw new InvalidOperationException($"HR_LEAVE_BALANCE.{column} has incompatible scalar metadata; repair stopped before DDL.");

            if (precision != 8 || scale != 4)
            {
                var presentColumnCount = 0L;
                foreach (var candidate in columns)
                {
                    await using var countColumn = connection.CreateCommand();
                    countColumn.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE' AND COLUMN_NAME=:columnName";
                    countColumn.Parameters.Add(new OracleParameter("columnName", candidate));
                    presentColumnCount += Convert.ToInt64(await countColumn.ExecuteScalarAsync());
                }
                if (presentColumnCount != columns.Length)
                    throw new InvalidOperationException($"HR_LEAVE_BALANCE.{column} needs NUMBER(8,4) alignment while only part of the precision group exists; repair stopped before DDL.");
            }

            await using var values = connection.CreateCommand();
            values.CommandText = $"SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE {column} IS NOT NULL AND (ABS({column})>=10000 OR {column}<>ROUND({column},4))";
            if (Convert.ToInt64(await values.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException($"HR_LEAVE_BALANCE.{column} contains values that cannot be represented by NUMBER(8,4); repair stopped before DDL.");
        }
    }

    private static async Task PreflightLegacyTableTransfers(OracleConnection connection)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        var tables = LegacyTableTransfers.SelectMany(m => new[] { m.Source, m.Target }).Distinct().ToArray();
        foreach (var table in tables)
            if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") != 1)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; legacy transfer repair stopped before DDL.");

        foreach (var map in LegacyTableTransfers)
        {
            foreach (var (sourceColumn, targetColumn) in map.Columns)
            {
                (string DataType, int? CharLength, string? CharUsed, int? Precision, int? Scale, string Nullable, string? Default, string Virtual)? source = null;
                (string DataType, int? CharLength, string? CharUsed, int? Precision, int? Scale, string Nullable, string? Default, string Virtual)? target = null;
                async Task<(string DataType, int? CharLength, string? CharUsed, int? Precision, int? Scale, string Nullable, string? Default, string Virtual)?> ReadColumn(string table, string column)
                {
                    await using var metadata = connection.CreateCommand();
                    metadata.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,CHAR_USED,DATA_PRECISION,DATA_SCALE,NULLABLE,DATA_DEFAULT,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName";
                    metadata.Parameters.Add(new OracleParameter("tableName", table));
                    metadata.Parameters.Add(new OracleParameter("columnName", column));
                    await using var reader = await metadata.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) return null;
                    var result = (reader.GetString(0), reader.IsDBNull(1) ? (int?)null : Convert.ToInt32(reader.GetValue(1)), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? (int?)null : Convert.ToInt32(reader.GetValue(3)), reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4)), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7));
                    if (await reader.ReadAsync()) throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} has ambiguous metadata; legacy transfer repair stopped before DDL.");
                    return result;
                }
                source = await ReadColumn(map.Source, sourceColumn);
                target = await ReadColumn(map.Target, targetColumn);
                if (source is null || target is null)
                    throw new InvalidOperationException($"Legacy transfer {map.Source}.{sourceColumn} -> {map.Target}.{targetColumn} has a missing column; repair stopped before DDL.");
                if (target.Value.Virtual == "YES")
                    throw new InvalidOperationException($"Legacy transfer target {map.Target}.{targetColumn} is virtual; repair stopped before DDL.");

                var sourceMeta = source.Value;
                var targetMeta = target.Value;
                var compatible = sourceMeta.DataType == targetMeta.DataType && sourceMeta.Virtual == "NO";
                if (sourceMeta.DataType is "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR")
                    compatible &= sourceMeta.CharUsed == targetMeta.CharUsed && sourceMeta.CharLength <= targetMeta.CharLength;
                else if (sourceMeta.DataType == "NUMBER")
                {
                    var sourceScale = sourceMeta.Scale ?? 0;
                    var targetScale = targetMeta.Scale ?? 0;
                    compatible &= targetMeta.Precision is null || (sourceMeta.Precision is not null && targetMeta.Precision.Value - targetScale >= sourceMeta.Precision.Value - sourceScale && targetScale >= sourceScale);
                }
                if (!compatible)
                    throw new InvalidOperationException($"Legacy transfer {map.Source}.{sourceColumn} is incompatible with {map.Target}.{targetColumn}; manual review is required before DDL.");

                if (targetMeta.Nullable == "N" && sourceMeta.Nullable != "N" && await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.{map.Source} WHERE {sourceColumn} IS NULL") != 0)
                    throw new InvalidOperationException($"Legacy transfer source {map.Source}.{sourceColumn} contains NULL values for a required target; repair stopped before DDL.");
            }

            if (await Count($"SELECT COUNT(*) FROM (SELECT ID FROM DEV_TEMPLATE.{map.Source} GROUP BY ID HAVING ID IS NULL OR COUNT(*)>1)") != 0)
                throw new InvalidOperationException($"Legacy transfer source {map.Source} has null or duplicate IDs; repair stopped before DDL.");

            var conflicts = string.Join(" OR ", map.Columns.Select(c => $"DECODE(s.{c.SourceColumn},t.{c.TargetColumn},0,1)=1"));
            if (await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.{map.Source} s JOIN DEV_TEMPLATE.{map.Target} t ON t.ID=s.ID WHERE {conflicts}") != 0)
                throw new InvalidOperationException($"Legacy transfer {map.Source} -> {map.Target} has same-ID rows with different mapped values; manual review is required before DDL.");
        }

        var lineRows = await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMP_SALARY_STRUCT_LINE");
        if (lineRows > 0)
        {
            if (await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMP_SALARY_STRUCT_LINE s WHERE NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.HR_EMP_SALARY_STRUCTURE p WHERE p.ID=s.STRUCTURE_ID) AND NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.HR_SALARY_STRUCTURE p WHERE p.ID=s.STRUCTURE_ID)") != 0)
                throw new InvalidOperationException("Legacy salary structure lines have no parent structure in either source or target; repair stopped before DDL.");
            if (await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMP_SALARY_STRUCT_LINE s WHERE NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.HR_SALARY_COMPONENT c WHERE c.COMPONENT_CODE=s.COMPONENT_CODE)") != 0)
                throw new InvalidOperationException("Legacy salary structure lines reference missing salary components; repair stopped before DDL.");
        }
    }
}
