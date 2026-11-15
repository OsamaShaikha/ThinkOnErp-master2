using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveCashoutSchema
{
    private sealed record NamedConstraint(string Table, string Name, string Type, string Columns = "", string? Condition = null);

    private static readonly NamedConstraint[] NonForeignConstraints =
    [
        new("HR_LEAVE_CASHOUT", "PK_HR_LEAVE_CASHOUT", "P", "ID"),
        new("HR_LEAVE_CASHOUT", "UX_HR_LEAVE_CASHOUT_KEY", "U", "SETTLEMENT_KEY"),
        new("HR_LEAVE_CASHOUT", "CK_HR_LEAVE_CASHOUT_STATUS", "C", Condition: "STATUS IN ('DRAFT','POSTED','REVERSED')"),
        new("HR_LEAVE_CASHOUT", "CK_HR_LEAVE_CASHOUT_DIVISOR", "C", Condition: "DAY_DIVISOR>0"),
        new("HR_LEAVE_CASHOUT", "CK_HR_LEAVE_CASHOUT_BASIS", "C", Condition: "WAGE_BASIS_CODE IN ('BASIC','GROSS_EARNINGS','SALARY_COMPONENT')"),
        new("HR_LEAVE_CASHOUT_LINE", "PK_HR_LEAVE_CASHOUT_LINE", "P", "ID"),
        new("HR_LEAVE_CASHOUT_LINE", "UX_HR_LEAVE_CASHOUT_LINE", "U", "SETTLEMENT_ID,LEAVE_YEAR_CLOSE_LINE_ID"),
        new("HR_LEAVE_CASHOUT_LINE", "CK_HR_LEAVE_CASHOUT_LINE_DAYS", "C", Condition: "CASHOUT_DAYS>0"),
        new("HR_LEAVE_CASHOUT_LINE", "CK_HR_LEAVE_CASHOUT_LINE_DIV", "C", Condition: "DAY_DIVISOR>0"),
        new("HR_LEAVE_CASHOUT_LINE", "CK_HR_LEAVE_CASHOUT_LINE_AMT", "C", Condition: "CASHOUT_AMOUNT>=0")
    ];

    private static readonly HrSchemaColumnExpectation[] OptionalColumns =
    [
        new("HR_LEAVE_CASHOUT", "POSTED_BY", "NVARCHAR2", "Y", CharacterLength: 200, Additive: true),
        new("HR_LEAVE_CASHOUT", "POSTED_DATE", "TIMESTAMP(7)", "Y", Additive: true),
        new("HR_LEAVE_CASHOUT", "REVERSED_BY", "NVARCHAR2", "Y", CharacterLength: 200, Additive: true),
        new("HR_LEAVE_CASHOUT", "REVERSED_DATE", "TIMESTAMP(7)", "Y", Additive: true),
        new("HR_LEAVE_CASHOUT", "REVERSAL_REASON", "NVARCHAR2", "Y", CharacterLength: 1000, Additive: true),
        new("HR_LEAVE_CASHOUT_LINE", "PAYROLL_ADJUSTMENT_ID", "NUMBER", "Y", Precision: 19, Scale: 0, Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 30;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        async Task Run(string sql)
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 30;
            await command.ExecuteNonQueryAsync();
        }
        async Task EnsureForeignKey(string table, string name, string localColumn, string referencedTable, string referencedColumn)
        {
            async Task<(string Type, string Status, string Validated, string DeleteRule, string Owner, string ReferencedTable)?> ReadConstraint()
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT C.CONSTRAINT_TYPE,C.STATUS,C.VALIDATED,C.DELETE_RULE,C.R_OWNER,RC.TABLE_NAME
                    FROM ALL_CONSTRAINTS C
                    LEFT JOIN ALL_CONSTRAINTS RC ON RC.OWNER=C.R_OWNER AND RC.CONSTRAINT_NAME=C.R_CONSTRAINT_NAME
                    WHERE C.OWNER='DEV_TEMPLATE' AND C.TABLE_NAME=:tableName AND C.CONSTRAINT_NAME=:constraintName
                    """;
                command.Parameters.Add(new OracleParameter("tableName", table));
                command.Parameters.Add(new OracleParameter("constraintName", name));
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;
                var result = (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4), reader.IsDBNull(5) ? string.Empty : reader.GetString(5));
                if (await reader.ReadAsync()) throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{name} is ambiguous.");
                return result;
            }

            async Task<string[]> ReadColumns(string owner, string targetTable, string constraintName)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS WHERE OWNER=:owner AND TABLE_NAME=:tableName AND CONSTRAINT_NAME=:constraintName ORDER BY POSITION";
                command.Parameters.Add(new OracleParameter("owner", owner));
                command.Parameters.Add(new OracleParameter("tableName", targetTable));
                command.Parameters.Add(new OracleParameter("constraintName", constraintName));
                var columns = new List<string>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
                return columns.ToArray();
            }

            var existing = await ReadConstraint();
            if (existing is null)
            {
                await Run($"ALTER TABLE DEV_TEMPLATE.{table} ADD CONSTRAINT {name} FOREIGN KEY ({localColumn}) REFERENCES DEV_TEMPLATE.{referencedTable}({referencedColumn})");
                existing = await ReadConstraint();
            }

            if (existing is null || existing.Value.Type != "R" || existing.Value.Owner != "DEV_TEMPLATE" ||
                existing.Value.ReferencedTable != referencedTable || existing.Value.DeleteRule != "NO ACTION" ||
                !(await ReadColumns("DEV_TEMPLATE", table, name)).SequenceEqual([localColumn], StringComparer.Ordinal) ||
                !(await ReadColumns(existing.Value.Owner, existing.Value.ReferencedTable, await ReadReferenceConstraintName())).SequenceEqual([referencedColumn], StringComparer.Ordinal))
                throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{name} does not match the expected foreign key to DEV_TEMPLATE.{referencedTable}({referencedColumn}).");

            if (existing.Value.Status != "ENABLED" || existing.Value.Validated != "VALIDATED")
                await Run($"ALTER TABLE DEV_TEMPLATE.{table} ENABLE VALIDATE CONSTRAINT {name}");

            var verified = await ReadConstraint();
            if (verified is null || verified.Value.Status != "ENABLED" || verified.Value.Validated != "VALIDATED")
                throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{name} is not ENABLED/VALIDATED after installation.");

            async Task<string> ReadReferenceConstraintName()
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT R_CONSTRAINT_NAME FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND CONSTRAINT_NAME=:constraintName";
                command.Parameters.Add(new OracleParameter("tableName", table));
                command.Parameters.Add(new OracleParameter("constraintName", name));
                return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
            }
        }

        foreach (var table in new[] { "HR_LEAVE_YEAR_CLOSE", "HR_LEAVE_YEAR_CLOSE_LINE", "HR_SALARY_STRUCTURE", "HR_SALARY_COMPONENT", "HR_PAYROLL_ADJUSTMENT" })
            if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") != 1)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; no cashout DDL applied.");

        await HrSchemaColumnGuard.ValidateAsync(connection, OptionalColumns, allowMissingColumns: true,
            allowMissingTables: new HashSet<string>(["HR_LEAVE_CASHOUT", "HR_LEAVE_CASHOUT_LINE"], StringComparer.Ordinal));
        await PreflightAdjustmentIndex(connection);
        await PreflightForeignKeys(connection);
        await ValidateNonForeignConstraints(connection, requireTables: false);

        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT'") == 0)
            await Run("CREATE TABLE DEV_TEMPLATE.HR_LEAVE_CASHOUT (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY, SETTLEMENT_KEY NVARCHAR2(200) NOT NULL, PAYLOAD_HASH NVARCHAR2(64) NOT NULL, LEAVE_YEAR_CLOSE_ID NUMBER(19) NOT NULL, PAY_PERIOD NVARCHAR2(40) NOT NULL, WAGE_BASIS_CODE NVARCHAR2(30) NOT NULL, BASIS_COMPONENT_CODE NVARCHAR2(100) NULL, DAY_DIVISOR NUMBER(10,4) NOT NULL, PAY_COMPONENT_CODE NVARCHAR2(100) NOT NULL, STATUS NVARCHAR2(20) NOT NULL, CREATION_USER NVARCHAR2(200) NOT NULL, CREATION_DATE TIMESTAMP(7) NOT NULL, CONSTRAINT PK_HR_LEAVE_CASHOUT PRIMARY KEY (ID), CONSTRAINT UX_HR_LEAVE_CASHOUT_KEY UNIQUE (SETTLEMENT_KEY), CONSTRAINT FK_HR_LEAVE_CASHOUT_CLOSE FOREIGN KEY (LEAVE_YEAR_CLOSE_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE(ID), CONSTRAINT FK_HR_LCO_PAY_COMP FOREIGN KEY (PAY_COMPONENT_CODE) REFERENCES DEV_TEMPLATE.HR_SALARY_COMPONENT(COMPONENT_CODE), CONSTRAINT FK_HR_LCO_BASIS_COMP FOREIGN KEY (BASIS_COMPONENT_CODE) REFERENCES DEV_TEMPLATE.HR_SALARY_COMPONENT(COMPONENT_CODE), CONSTRAINT CK_HR_LEAVE_CASHOUT_STATUS CHECK (STATUS IN ('DRAFT','POSTED','REVERSED')), CONSTRAINT CK_HR_LEAVE_CASHOUT_DIVISOR CHECK (DAY_DIVISOR>0), CONSTRAINT CK_HR_LEAVE_CASHOUT_BASIS CHECK (WAGE_BASIS_CODE IN ('BASIC','GROSS_EARNINGS','SALARY_COMPONENT')))" );
        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT_LINE'") == 0)
            await Run("CREATE TABLE DEV_TEMPLATE.HR_LEAVE_CASHOUT_LINE (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY, SETTLEMENT_ID NUMBER(19) NOT NULL, LEAVE_YEAR_CLOSE_LINE_ID NUMBER(19) NOT NULL, EMPLOYEE_CODE NVARCHAR2(100) NOT NULL, LEAVE_TYPE_CODE NVARCHAR2(100) NOT NULL, SALARY_STRUCTURE_ID NUMBER(19) NOT NULL, SALARY_EFFECTIVE_FROM DATE NOT NULL, CASHOUT_DAYS NUMBER(8,4) NOT NULL, WAGE_BASIS_AMOUNT NUMBER(18,4) NOT NULL, DAY_DIVISOR NUMBER(10,4) NOT NULL, CASHOUT_AMOUNT NUMBER(18,4) NOT NULL, CURRENCY_CODE NVARCHAR2(20) NOT NULL, BASIS_SNAPSHOT_JSON NCLOB NOT NULL, CONSTRAINT PK_HR_LEAVE_CASHOUT_LINE PRIMARY KEY (ID), CONSTRAINT UX_HR_LEAVE_CASHOUT_LINE UNIQUE (SETTLEMENT_ID,LEAVE_YEAR_CLOSE_LINE_ID), CONSTRAINT FK_HR_LEAVE_CASHOUT_LINE_HDR FOREIGN KEY (SETTLEMENT_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_CASHOUT(ID) ON DELETE CASCADE, CONSTRAINT FK_HR_LEAVE_CASHOUT_LINE_CLOSE FOREIGN KEY (LEAVE_YEAR_CLOSE_LINE_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE(ID), CONSTRAINT FK_HR_LCO_LINE_SALARY FOREIGN KEY (SALARY_STRUCTURE_ID) REFERENCES DEV_TEMPLATE.HR_SALARY_STRUCTURE(ID), CONSTRAINT CK_HR_LEAVE_CASHOUT_LINE_DAYS CHECK (CASHOUT_DAYS>0), CONSTRAINT CK_HR_LEAVE_CASHOUT_LINE_DIV CHECK (DAY_DIVISOR>0), CONSTRAINT CK_HR_LEAVE_CASHOUT_LINE_AMT CHECK (CASHOUT_AMOUNT>=0))");
        foreach (var (table, column, definition) in new[]
        {
            ("HR_LEAVE_CASHOUT", "POSTED_BY", "NVARCHAR2(200) NULL"),
            ("HR_LEAVE_CASHOUT", "POSTED_DATE", "TIMESTAMP(7) NULL"),
            ("HR_LEAVE_CASHOUT", "REVERSED_BY", "NVARCHAR2(200) NULL"),
            ("HR_LEAVE_CASHOUT", "REVERSED_DATE", "TIMESTAMP(7) NULL"),
            ("HR_LEAVE_CASHOUT", "REVERSAL_REASON", "NVARCHAR2(1000) NULL"),
            ("HR_LEAVE_CASHOUT_LINE", "PAYROLL_ADJUSTMENT_ID", "NUMBER(19) NULL")
        })
        {
            if (await Count($"SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='{column}'") == 0)
                await Run($"ALTER TABLE DEV_TEMPLATE.{table} ADD ({column} {definition})");
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, OptionalColumns, allowMissingColumns: false);
        await ValidateNonForeignConstraints(connection, requireTables: true);
        await EnsureForeignKey("HR_LEAVE_CASHOUT", "FK_HR_LCO_PAY_COMP", "PAY_COMPONENT_CODE", "HR_SALARY_COMPONENT", "COMPONENT_CODE");
        await EnsureForeignKey("HR_LEAVE_CASHOUT", "FK_HR_LCO_BASIS_COMP", "BASIS_COMPONENT_CODE", "HR_SALARY_COMPONENT", "COMPONENT_CODE");
        var legacySalaryConstraint = await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT_LINE' AND CONSTRAINT_NAME='FK_HR_LEAVE_CASHOUT_LINE_SALARY'") > 0;
        var salaryConstraint = legacySalaryConstraint ? "FK_HR_LEAVE_CASHOUT_LINE_SALARY" : "FK_HR_LCO_LINE_SALARY";
        await EnsureForeignKey("HR_LEAVE_CASHOUT_LINE", salaryConstraint, "SALARY_STRUCTURE_ID", "HR_SALARY_STRUCTURE", "ID");
        await EnsureForeignKey("HR_LEAVE_CASHOUT_LINE", "FK_HR_LCO_LINE_PAY_ADJ", "PAYROLL_ADJUSTMENT_ID", "HR_PAYROLL_ADJUSTMENT", "ID");
        if (await Count("SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT_LINE' AND INDEX_NAME='UX_HR_LCO_LINE_ADJ'") == 0)
            await Run("CREATE UNIQUE INDEX DEV_TEMPLATE.UX_HR_LCO_LINE_ADJ ON DEV_TEMPLATE.HR_LEAVE_CASHOUT_LINE(PAYROLL_ADJUSTMENT_ID)");
        report.Add("Cashout settlements retain reviewable salary snapshots and link idempotently to pending payroll adjustments; HR does not create accounting entries or payments.");
        await Verify(connection, report);
    }

    private static async Task PreflightAdjustmentIndex(OracleConnection connection)
    {
        const string indexName = "UX_HR_LCO_LINE_ADJ";
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        await using (var index = connection.CreateCommand())
        {
            index.CommandText = "SELECT TABLE_NAME,UNIQUENESS,STATUS FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME=:indexName";
            index.Parameters.Add(new OracleParameter("indexName", indexName));
            string? indexedTable = null;
            string? uniqueness = null;
            string? status = null;
            await using (var reader = await index.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    indexedTable = reader.GetString(0);
                    uniqueness = reader.GetString(1);
                    status = reader.GetString(2);
                    if (await reader.ReadAsync())
                        throw new InvalidOperationException($"DEV_TEMPLATE.{indexName} is ambiguous; cashout repair stopped before DDL.");
                }
            }
            if (indexedTable is not null)
            {
                if (indexedTable != "HR_LEAVE_CASHOUT_LINE" || uniqueness != "UNIQUE" || status != "VALID")
                    throw new InvalidOperationException($"DEV_TEMPLATE.{indexName} is attached to another object or has incompatible metadata; cashout repair stopped before DDL.");
                await using var columns = connection.CreateCommand();
                columns.CommandText = "SELECT COLUMN_NAME FROM ALL_IND_COLUMNS WHERE INDEX_OWNER='DEV_TEMPLATE' AND INDEX_NAME=:indexName ORDER BY COLUMN_POSITION";
                columns.Parameters.Add(new OracleParameter("indexName", indexName));
                var actualColumns = new List<string>();
                await using (var columnReader = await columns.ExecuteReaderAsync())
                    while (await columnReader.ReadAsync()) actualColumns.Add(columnReader.GetString(0));
                if (!actualColumns.SequenceEqual(["PAYROLL_ADJUSTMENT_ID"], StringComparer.Ordinal))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{indexName} does not index only PAYROLL_ADJUSTMENT_ID; cashout repair stopped before DDL.");
            }
        }

        var tableExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT_LINE'") != 0;
        var columnExists = await Count("SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT_LINE' AND COLUMN_NAME='PAYROLL_ADJUSTMENT_ID'") != 0;
        if (tableExists && columnExists && await Count("SELECT COUNT(*) FROM (SELECT PAYROLL_ADJUSTMENT_ID FROM DEV_TEMPLATE.HR_LEAVE_CASHOUT_LINE WHERE PAYROLL_ADJUSTMENT_ID IS NOT NULL GROUP BY PAYROLL_ADJUSTMENT_ID HAVING COUNT(*)>1)") != 0)
            throw new InvalidOperationException("HR_LEAVE_CASHOUT_LINE has duplicate non-null PAYROLL_ADJUSTMENT_ID values; cashout repair stopped before DDL.");
    }

    private static async Task PreflightForeignKeys(OracleConnection connection)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        async Task<string[]> Columns(string owner, string table, string constraint)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS WHERE OWNER=:owner AND TABLE_NAME=:tableName AND CONSTRAINT_NAME=:constraintName ORDER BY POSITION";
            command.Parameters.Add(new OracleParameter("owner", owner));
            command.Parameters.Add(new OracleParameter("tableName", table));
            command.Parameters.Add(new OracleParameter("constraintName", constraint));
            var result = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add(reader.GetString(0));
            return result.ToArray();
        }

        var legacySalaryConstraint = await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT_LINE' AND CONSTRAINT_NAME='FK_HR_LEAVE_CASHOUT_LINE_SALARY'") != 0;
        if (!legacySalaryConstraint && await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='FK_HR_LEAVE_CASHOUT_LINE_SALARY'") != 0)
            throw new InvalidOperationException("DEV_TEMPLATE.FK_HR_LEAVE_CASHOUT_LINE_SALARY is reserved by another table; cashout repair stopped before DDL.");

        var plans = new[]
        {
            (Table:"HR_LEAVE_CASHOUT", Name:"FK_HR_LEAVE_CASHOUT_CLOSE", Local:"LEAVE_YEAR_CLOSE_ID", Parent:"HR_LEAVE_YEAR_CLOSE", Referenced:"ID", DeleteRule:"NO ACTION", Repair:false),
            (Table:"HR_LEAVE_CASHOUT", Name:"FK_HR_LCO_PAY_COMP", Local:"PAY_COMPONENT_CODE", Parent:"HR_SALARY_COMPONENT", Referenced:"COMPONENT_CODE", DeleteRule:"NO ACTION", Repair:true),
            (Table:"HR_LEAVE_CASHOUT", Name:"FK_HR_LCO_BASIS_COMP", Local:"BASIS_COMPONENT_CODE", Parent:"HR_SALARY_COMPONENT", Referenced:"COMPONENT_CODE", DeleteRule:"NO ACTION", Repair:true),
            (Table:"HR_LEAVE_CASHOUT_LINE", Name:"FK_HR_LEAVE_CASHOUT_LINE_HDR", Local:"SETTLEMENT_ID", Parent:"HR_LEAVE_CASHOUT", Referenced:"ID", DeleteRule:"CASCADE", Repair:false),
            (Table:"HR_LEAVE_CASHOUT_LINE", Name:"FK_HR_LEAVE_CASHOUT_LINE_CLOSE", Local:"LEAVE_YEAR_CLOSE_LINE_ID", Parent:"HR_LEAVE_YEAR_CLOSE_LINE", Referenced:"ID", DeleteRule:"NO ACTION", Repair:false),
            (Table:"HR_LEAVE_CASHOUT_LINE", Name:legacySalaryConstraint ? "FK_HR_LEAVE_CASHOUT_LINE_SALARY" : "FK_HR_LCO_LINE_SALARY", Local:"SALARY_STRUCTURE_ID", Parent:"HR_SALARY_STRUCTURE", Referenced:"ID", DeleteRule:"NO ACTION", Repair:true),
            (Table:"HR_LEAVE_CASHOUT_LINE", Name:"FK_HR_LCO_LINE_PAY_ADJ", Local:"PAYROLL_ADJUSTMENT_ID", Parent:"HR_PAYROLL_ADJUSTMENT", Referenced:"ID", DeleteRule:"NO ACTION", Repair:true)
        };

        foreach (var plan in plans)
        {
            var childExists = await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{plan.Table}'") != 0;
            var localExists = childExists && await Count($"SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{plan.Table}' AND COLUMN_NAME='{plan.Local}'") != 0;
            if (childExists && !localExists && plan.Local != "PAYROLL_ADJUSTMENT_ID")
                throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Table}.{plan.Local} is missing; cashout repair stopped before DDL.");

            var parentTableExists = await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{plan.Parent}'") == 1;
            if (!parentTableExists)
            {
                var bothCashoutTablesWillBeCreated = plan.Parent == "HR_LEAVE_CASHOUT" &&
                    await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME IN ('HR_LEAVE_CASHOUT','HR_LEAVE_CASHOUT_LINE')") == 0;
                if (!bothCashoutTablesWillBeCreated)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Parent} is missing and is not created by this cashout batch; no DDL applied.");
            }
            else if (await Count($"SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{plan.Parent}' AND CONSTRAINT_TYPE IN ('P','U') AND STATUS='ENABLED' AND VALIDATED='VALIDATED' AND CONSTRAINT_NAME IN (SELECT CONSTRAINT_NAME FROM ALL_CONS_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{plan.Parent}' AND COLUMN_NAME='{plan.Referenced}' GROUP BY CONSTRAINT_NAME HAVING COUNT(*)=1)") == 0)
                throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Parent}.{plan.Referenced} has no enabled, validated single-column primary/unique key; cashout repair stopped before DDL.");

            string? localTable = null;
            string? type = null;
            string? status = null;
            string? validated = null;
            string? deleteRule = null;
            string? referencedOwner = null;
            string? referencedName = null;
            await using (var named = connection.CreateCommand())
            {
                named.CommandText = "SELECT TABLE_NAME,CONSTRAINT_TYPE,STATUS,VALIDATED,DELETE_RULE,R_OWNER,R_CONSTRAINT_NAME FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
                named.Parameters.Add(new OracleParameter("constraintName", plan.Name));
                await using var reader = await named.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    localTable = reader.GetString(0);
                    type = reader.GetString(1);
                    status = reader.GetString(2);
                    validated = reader.GetString(3);
                    deleteRule = reader.GetString(4);
                    referencedOwner = reader.IsDBNull(5) ? null : reader.GetString(5);
                    referencedName = reader.IsDBNull(6) ? null : reader.GetString(6);
                    if (await reader.ReadAsync())
                        throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Name} is ambiguous; cashout repair stopped before DDL.");
                }
            }
            if (localTable is not null)
            {
                if (localTable != plan.Table || type != "R" || deleteRule != plan.DeleteRule || referencedOwner != "DEV_TEMPLATE" || string.IsNullOrEmpty(referencedName))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Name} is attached to another object or has incompatible FK metadata; cashout repair stopped before DDL.");
                if (!(await Columns("DEV_TEMPLATE", plan.Table, plan.Name)).SequenceEqual([plan.Local], StringComparer.Ordinal))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Name} has unexpected local columns; cashout repair stopped before DDL.");
                if (await Count($"SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{plan.Parent}' AND CONSTRAINT_NAME='{referencedName}' AND CONSTRAINT_TYPE IN ('P','U')") == 0 ||
                    !(await Columns("DEV_TEMPLATE", plan.Parent, referencedName)).SequenceEqual([plan.Referenced], StringComparer.Ordinal))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Name} references unexpected columns; cashout repair stopped before DDL.");
                if (status is not ("ENABLED" or "DISABLED") || validated is not ("VALIDATED" or "NOT VALIDATED"))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Name} has unsupported constraint state; cashout repair stopped before DDL.");
            }
            else if (childExists && !plan.Repair)
                throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Table}.{plan.Name} is missing and this installer cannot safely recreate it on an existing table; cashout DDL stopped before changes.");

            if (childExists && localExists && await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.{plan.Table} s WHERE s.{plan.Local} IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.{plan.Parent} p WHERE p.{plan.Referenced}=s.{plan.Local})") != 0)
                throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Table}.{plan.Local} contains orphan values for {plan.Parent}.{plan.Referenced}; cashout repair stopped before DDL.");
        }
    }

    private static async Task ValidateNonForeignConstraints(OracleConnection connection, bool requireTables)
    {
        foreach (var expected in NonForeignConstraints)
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
            tableCommand.Parameters.Add(new OracleParameter("tableName", expected.Table));
            var tableExists = Convert.ToInt32(await tableCommand.ExecuteScalarAsync()) == 1;
            if (!tableExists)
            {
                if (requireTables)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{expected.Table} is missing while verifying {expected.Name}.");
                await using var collision = connection.CreateCommand();
                collision.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
                collision.Parameters.Add(new OracleParameter("constraintName", expected.Name));
                if (Convert.ToInt32(await collision.ExecuteScalarAsync()) != 0)
                    throw new InvalidOperationException($"Constraint name {expected.Name} is already used while its target table is absent; cashout DDL stopped before changes.");
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TABLE_NAME,CONSTRAINT_TYPE,STATUS,VALIDATED,SEARCH_CONDITION_VC FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Add(new OracleParameter("constraintName", expected.Name));
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"DEV_TEMPLATE.{expected.Table}.{expected.Name} is missing; installer cannot repair this primary, unique or check constraint safely.");
            var table = reader.GetString(0);
            var type = reader.GetString(1);
            var status = reader.GetString(2);
            var validated = reader.GetString(3);
            var condition = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            if (await reader.ReadAsync() || table != expected.Table || type != expected.Type || status != "ENABLED" || validated != "VALIDATED")
                throw new InvalidOperationException($"DEV_TEMPLATE.{expected.Name} is attached to an unexpected table/type or is not enabled and validated; no cashout DDL applied.");
            await reader.DisposeAsync();

            if (expected.Type is "P" or "U")
            {
                await using var columns = connection.CreateCommand();
                columns.CommandText = "SELECT LISTAGG(COLUMN_NAME,',') WITHIN GROUP (ORDER BY POSITION) FROM ALL_CONS_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND CONSTRAINT_NAME=:constraintName";
                columns.Parameters.Add(new OracleParameter("tableName", expected.Table));
                columns.Parameters.Add(new OracleParameter("constraintName", expected.Name));
                if (!string.Equals(Convert.ToString(await columns.ExecuteScalarAsync()), expected.Columns, StringComparison.Ordinal))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{expected.Name} has an incompatible ordered column signature; no cashout DDL applied.");
            }
            else if (NormalizeCondition(condition) != NormalizeCondition(expected.Condition ?? string.Empty))
                throw new InvalidOperationException($"DEV_TEMPLATE.{expected.Name} has an incompatible condition; no cashout DDL applied.");
        }
    }

    private static string NormalizeCondition(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '(' && c != ')' && c != '"').Select(char.ToUpperInvariant).ToArray());

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 30;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["HR_LEAVE_CASHOUT"] = ["ID","SETTLEMENT_KEY","PAYLOAD_HASH","LEAVE_YEAR_CLOSE_ID","PAY_PERIOD","WAGE_BASIS_CODE","BASIS_COMPONENT_CODE","DAY_DIVISOR","PAY_COMPONENT_CODE","STATUS","CREATION_USER","CREATION_DATE","POSTED_BY","POSTED_DATE","REVERSED_BY","REVERSED_DATE","REVERSAL_REASON"],
            ["HR_LEAVE_CASHOUT_LINE"] = ["ID","SETTLEMENT_ID","LEAVE_YEAR_CLOSE_LINE_ID","EMPLOYEE_CODE","LEAVE_TYPE_CODE","SALARY_STRUCTURE_ID","SALARY_EFFECTIVE_FROM","CASHOUT_DAYS","WAGE_BASIS_AMOUNT","DAY_DIVISOR","CASHOUT_AMOUNT","CURRENCY_CODE","BASIS_SNAPSHOT_JSON","PAYROLL_ADJUSTMENT_ID"]
        };
        foreach (var (table, columns) in expected)
        {
            await using var metadata = connection.CreateCommand();
            metadata.CommandText = "SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName ORDER BY COLUMN_ID";
            metadata.Parameters.Add(new OracleParameter("tableName", table));
            var actual = new List<string>();
            await using (var reader = await metadata.ExecuteReaderAsync())
                while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
            if (!actual.SequenceEqual(columns, StringComparer.Ordinal))
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} columns are missing or have an unexpected shape.");

            await using var mapped = connection.CreateCommand();
            mapped.CommandText = $"SELECT {string.Join(",", columns.Select(c => "\"" + c + "\""))} FROM DEV_TEMPLATE.\"{table}\" WHERE 1=0";
            await using var mappedReader = await mapped.ExecuteReaderAsync();
            if (mappedReader.FieldCount != columns.Length) throw new InvalidOperationException($"Mapped select failed for DEV_TEMPLATE.{table}.");
            report.Add($"{table}: {columns.Length} columns and mapped Oracle SELECT verified.");
        }

        var legacySalaryConstraint = await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_CASHOUT_LINE' AND CONSTRAINT_NAME='FK_HR_LEAVE_CASHOUT_LINE_SALARY'") > 0;
        var salaryConstraint = legacySalaryConstraint ? "FK_HR_LEAVE_CASHOUT_LINE_SALARY" : "FK_HR_LCO_LINE_SALARY";
        var constraints = new[]
        {
            ("HR_LEAVE_CASHOUT", "PK_HR_LEAVE_CASHOUT", "P"), ("HR_LEAVE_CASHOUT", "UX_HR_LEAVE_CASHOUT_KEY", "U"),
            ("HR_LEAVE_CASHOUT", "FK_HR_LEAVE_CASHOUT_CLOSE", "R"), ("HR_LEAVE_CASHOUT", "FK_HR_LCO_PAY_COMP", "R"),
            ("HR_LEAVE_CASHOUT", "FK_HR_LCO_BASIS_COMP", "R"), ("HR_LEAVE_CASHOUT", "CK_HR_LEAVE_CASHOUT_STATUS", "C"),
            ("HR_LEAVE_CASHOUT", "CK_HR_LEAVE_CASHOUT_DIVISOR", "C"), ("HR_LEAVE_CASHOUT", "CK_HR_LEAVE_CASHOUT_BASIS", "C"),
            ("HR_LEAVE_CASHOUT_LINE", "PK_HR_LEAVE_CASHOUT_LINE", "P"), ("HR_LEAVE_CASHOUT_LINE", "UX_HR_LEAVE_CASHOUT_LINE", "U"),
            ("HR_LEAVE_CASHOUT_LINE", "FK_HR_LEAVE_CASHOUT_LINE_HDR", "R"), ("HR_LEAVE_CASHOUT_LINE", "FK_HR_LEAVE_CASHOUT_LINE_CLOSE", "R"),
            ("HR_LEAVE_CASHOUT_LINE", "FK_HR_LCO_LINE_PAY_ADJ", "R"),
            ("HR_LEAVE_CASHOUT_LINE", salaryConstraint, "R"), ("HR_LEAVE_CASHOUT_LINE", "CK_HR_LEAVE_CASHOUT_LINE_DAYS", "C"),
            ("HR_LEAVE_CASHOUT_LINE", "CK_HR_LEAVE_CASHOUT_LINE_DIV", "C"), ("HR_LEAVE_CASHOUT_LINE", "CK_HR_LEAVE_CASHOUT_LINE_AMT", "C")
        };
        foreach (var (table, name, expectedType) in constraints)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CONSTRAINT_TYPE,STATUS,VALIDATED FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Add(new OracleParameter("tableName", table)); command.Parameters.Add(new OracleParameter("constraintName", name));
            await using var reader = await command.ExecuteReaderAsync();
            var found = await reader.ReadAsync();
            var constraintType = found ? reader.GetString(0) : "MISSING";
            var status = found ? reader.GetString(1) : "MISSING";
            var validated = found ? reader.GetString(2) : "MISSING";
            if (!found || constraintType != expectedType || status != "ENABLED" || validated != "VALIDATED" || await reader.ReadAsync())
                throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{name} is {constraintType}/{status}/{validated}; expected {expectedType}/ENABLED/VALIDATED.");
        }
        if (await Count("""
            SELECT COUNT(*) FROM ALL_INDEXES i
            WHERE i.OWNER='DEV_TEMPLATE' AND i.TABLE_NAME='HR_LEAVE_CASHOUT_LINE'
              AND i.INDEX_NAME='UX_HR_LCO_LINE_ADJ' AND i.UNIQUENESS='UNIQUE'
              AND i.STATUS='VALID' AND i.INDEX_TYPE='NORMAL'
              AND (SELECT LISTAGG(c.COLUMN_NAME,',') WITHIN GROUP (ORDER BY c.COLUMN_POSITION)
                   FROM ALL_IND_COLUMNS c WHERE c.INDEX_OWNER=i.OWNER AND c.INDEX_NAME=i.INDEX_NAME)='PAYROLL_ADJUSTMENT_ID'
              AND NOT EXISTS (SELECT 1 FROM ALL_IND_EXPRESSIONS e WHERE e.INDEX_OWNER=i.OWNER AND e.INDEX_NAME=i.INDEX_NAME)
            """) != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_CASHOUT_LINE.UX_HR_LCO_LINE_ADJ is missing or differs from the valid normal unique PAYROLL_ADJUSTMENT_ID index.");
        report.Add($"{constraints.Length} cashout primary, unique, foreign-key and check constraints are enabled and validated.");
    }
}
