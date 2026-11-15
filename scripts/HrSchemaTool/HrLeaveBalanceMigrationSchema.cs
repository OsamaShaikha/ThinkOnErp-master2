using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveBalanceMigrationSchema
{
    private sealed record ConstraintExpectation(string Name, string Table, string Type, string Columns, string? ParentTable = null, string? ParentColumns = null, string? Condition = null);

    private static readonly ConstraintExpectation[] Constraints =
    [
        new("PK_HR_BAL_MIGRATION", "HR_LEAVE_BALANCE_MIGRATION", "P", "ID"),
        new("UX_HR_BAL_MIGRATION_KEY", "HR_LEAVE_BALANCE_MIGRATION", "U", "MIGRATION_KEY"),
        new("CK_HR_BAL_MIGRATION_STATUS", "HR_LEAVE_BALANCE_MIGRATION", "C", "", Condition: "STATUS IN ('DRAFT','POSTED','REVERSED')"),
        new("CK_HR_BAL_MIGRATION_MONTHS", "HR_LEAVE_BALANCE_MIGRATION", "C", "", Condition: "SOURCE_YEAR_START_MONTH BETWEEN 1 AND 12 AND TARGET_YEAR_START_MONTH BETWEEN 1 AND 12"),
        new("CK_HR_BAL_MIGRATION_DAYS", "HR_LEAVE_BALANCE_MIGRATION", "C", "", Condition: "SOURCE_YEAR_START_DAY BETWEEN 1 AND 31 AND TARGET_YEAR_START_DAY BETWEEN 1 AND 31"),
        new("PK_HR_BAL_MIGRATION_LINE", "HR_LEAVE_BALANCE_MIGRATION_LINE", "P", "ID"),
        new("UX_HR_BAL_MIGRATION_LINE", "HR_LEAVE_BALANCE_MIGRATION_LINE", "U", "MIGRATION_ID,SOURCE_BALANCE_ID,TARGET_CYCLE_START_DATE"),
        new("FK_HR_BAL_MIG_LINE_BATCH", "HR_LEAVE_BALANCE_MIGRATION_LINE", "R", "MIGRATION_ID", "HR_LEAVE_BALANCE_MIGRATION", "ID"),
        new("FK_HR_BAL_MIG_LINE_SRC", "HR_LEAVE_BALANCE_MIGRATION_LINE", "R", "SOURCE_BALANCE_ID", "HR_LEAVE_BALANCE", "ID"),
        new("FK_HR_BAL_MIG_LINE_TGT", "HR_LEAVE_BALANCE_MIGRATION_LINE", "R", "TARGET_BALANCE_ID", "HR_LEAVE_BALANCE", "ID"),
        new("FK_HR_BAL_MIG_LINE_SRCMOV", "HR_LEAVE_BALANCE_MIGRATION_LINE", "R", "SOURCE_MOVEMENT_ID", "HR_LEAVE_MOVEMENT", "ID"),
        new("FK_HR_BAL_MIG_LINE_TGTMOV", "HR_LEAVE_BALANCE_MIGRATION_LINE", "R", "TARGET_MOVEMENT_ID", "HR_LEAVE_MOVEMENT", "ID"),
        new("CK_HR_BAL_MIG_LINE_DATES", "HR_LEAVE_BALANCE_MIGRATION_LINE", "C", "", Condition: "SOURCE_CYCLE_END_DATE>=SOURCE_CYCLE_START_DATE AND TARGET_CYCLE_END_DATE>=TARGET_CYCLE_START_DATE"),
        new("CK_HR_BAL_MIG_LINE_SOURCE_NULLS", "HR_LEAVE_BALANCE_MIGRATION_LINE", "C", "", Condition: "SOURCE_START_WAS_NULL IN (0,1) AND SOURCE_END_WAS_NULL IN (0,1)")
    ];

    private static readonly HrSchemaColumnExpectation[] MigrationColumns =
    [
        new("HR_LEAVE_BALANCE_MIGRATION", "ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION", "MIGRATION_KEY", "NVARCHAR2", "N", CharacterLength: 200),
        new("HR_LEAVE_BALANCE_MIGRATION", "EFFECTIVE_DATE", "DATE", "N"),
        new("HR_LEAVE_BALANCE_MIGRATION", "SOURCE_YEAR_START_MONTH", "NUMBER", "N", Precision: 2, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION", "SOURCE_YEAR_START_DAY", "NUMBER", "N", Precision: 2, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION", "TARGET_YEAR_START_MONTH", "NUMBER", "N", Precision: 2, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION", "TARGET_YEAR_START_DAY", "NUMBER", "N", Precision: 2, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION", "PAYLOAD_HASH", "NVARCHAR2", "N", CharacterLength: 64),
        new("HR_LEAVE_BALANCE_MIGRATION", "STATUS", "NVARCHAR2", "N", CharacterLength: 20),
        new("HR_LEAVE_BALANCE_MIGRATION", "CREATION_USER", "NVARCHAR2", "N", CharacterLength: 200),
        new("HR_LEAVE_BALANCE_MIGRATION", "CREATION_DATE", "TIMESTAMP(7)", "N"),
        new("HR_LEAVE_BALANCE_MIGRATION", "POSTED_BY", "NVARCHAR2", "Y", CharacterLength: 200),
        new("HR_LEAVE_BALANCE_MIGRATION", "POSTED_DATE", "TIMESTAMP(7)", "Y"),
        new("HR_LEAVE_BALANCE_MIGRATION", "REVERSED_BY", "NVARCHAR2", "Y", CharacterLength: 200),
        new("HR_LEAVE_BALANCE_MIGRATION", "REVERSED_DATE", "TIMESTAMP(7)", "Y"),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "MIGRATION_ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_BALANCE_ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "TARGET_BALANCE_ID", "NUMBER", "Y", Precision: 19, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "EMPLOYEE_CODE", "NVARCHAR2", "N", CharacterLength: 100),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "LEAVE_TYPE_CODE", "NVARCHAR2", "N", CharacterLength: 100),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_CYCLE_START_DATE", "DATE", "N"),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_CYCLE_END_DATE", "DATE", "N"),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_YEAR_NO", "NUMBER", "N", Precision: 4, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_START_WAS_NULL", "NUMBER", "N", Precision: 1, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_END_WAS_NULL", "NUMBER", "N", Precision: 1, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_ACCRUED_DAYS", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_USED_DAYS", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_CARRIED_DAYS", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "TARGET_CYCLE_START_DATE", "DATE", "N"),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "TARGET_CYCLE_END_DATE", "DATE", "N"),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "ALLOCATED_ACCRUED_DAYS", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "ALLOCATED_USED_DAYS", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "ALLOCATED_CARRIED_DAYS", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "TARGET_ACCRUED_BEFORE", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "TARGET_USED_BEFORE", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "TARGET_CARRIED_BEFORE", "NUMBER", "N", Precision: 8, Scale: 4),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "SOURCE_MOVEMENT_ID", "NUMBER", "Y", Precision: 19, Scale: 0),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "TARGET_MOVEMENT_ID", "NUMBER", "Y", Precision: 19, Scale: 0)
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

        foreach (var table in new[] { "HR_LEAVE_BALANCE", "HR_LEAVE_MOVEMENT", "HR_LEAVE_SETTINGS" })
            if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") != 1)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; no migration DDL applied.");

        var headerExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION'") == 1;
        var lineExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE'") == 1;
        if (lineExists && !headerExists)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_BALANCE_MIGRATION_LINE exists without its parent; no migration DDL applied.");
        await ValidateIdentityColumns(connection, allowMissingTables: true);

        if (!headerExists || !lineExists)
        {
            var reservedIndexNames = new[]
            {
                "PK_HR_BAL_MIGRATION", "UX_HR_BAL_MIGRATION_KEY",
                "PK_HR_BAL_MIGRATION_LINE", "UX_HR_BAL_MIGRATION_LINE"
            };
            await using var indexCollision = connection.CreateCommand();
            indexCollision.CommandText = "SELECT INDEX_NAME,TABLE_NAME FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME IN ('PK_HR_BAL_MIGRATION','UX_HR_BAL_MIGRATION_KEY','PK_HR_BAL_MIGRATION_LINE','UX_HR_BAL_MIGRATION_LINE')";
            await using var indexReader = await indexCollision.ExecuteReaderAsync();
            while (await indexReader.ReadAsync())
            {
                var indexName = indexReader.GetString(0);
                var tableName = indexReader.GetString(1);
                var belongsToMissingTarget = reservedIndexNames.Contains(indexName, StringComparer.Ordinal)
                    && (indexName.StartsWith("PK_HR_BAL_MIGRATION_LINE", StringComparison.Ordinal) || indexName == "UX_HR_BAL_MIGRATION_LINE"
                        ? !lineExists && tableName != "HR_LEAVE_BALANCE_MIGRATION_LINE"
                        : !headerExists && tableName != "HR_LEAVE_BALANCE_MIGRATION");
                if (belongsToMissingTarget)
                    throw new InvalidOperationException($"Reserved migration index name {indexName} is already used by {tableName}; no migration DDL applied.");
            }
        }

        await HrSchemaColumnGuard.ValidateAsync(connection, MigrationColumns, allowMissingColumns: false,
            allowMissingTables: new HashSet<string>(["HR_LEAVE_BALANCE_MIGRATION", "HR_LEAVE_BALANCE_MIGRATION_LINE"], StringComparer.Ordinal));
        await VerifyExistingStringDefinitions(connection);
        await PreflightConstraints(connection);

        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION'") == 0)
        {
            await Run("CREATE TABLE DEV_TEMPLATE.HR_LEAVE_BALANCE_MIGRATION (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY, MIGRATION_KEY NVARCHAR2(200) NOT NULL, EFFECTIVE_DATE DATE NOT NULL, SOURCE_YEAR_START_MONTH NUMBER(2) NOT NULL, SOURCE_YEAR_START_DAY NUMBER(2) NOT NULL, TARGET_YEAR_START_MONTH NUMBER(2) NOT NULL, TARGET_YEAR_START_DAY NUMBER(2) NOT NULL, PAYLOAD_HASH NVARCHAR2(64) NOT NULL, STATUS NVARCHAR2(20) NOT NULL, CREATION_USER NVARCHAR2(200) NOT NULL, CREATION_DATE TIMESTAMP(7) NOT NULL, POSTED_BY NVARCHAR2(200), POSTED_DATE TIMESTAMP(7), REVERSED_BY NVARCHAR2(200), REVERSED_DATE TIMESTAMP(7), CONSTRAINT PK_HR_BAL_MIGRATION PRIMARY KEY (ID), CONSTRAINT UX_HR_BAL_MIGRATION_KEY UNIQUE (MIGRATION_KEY), CONSTRAINT CK_HR_BAL_MIGRATION_STATUS CHECK (STATUS IN ('DRAFT','POSTED','REVERSED')), CONSTRAINT CK_HR_BAL_MIGRATION_MONTHS CHECK (SOURCE_YEAR_START_MONTH BETWEEN 1 AND 12 AND TARGET_YEAR_START_MONTH BETWEEN 1 AND 12), CONSTRAINT CK_HR_BAL_MIGRATION_DAYS CHECK (SOURCE_YEAR_START_DAY BETWEEN 1 AND 31 AND TARGET_YEAR_START_DAY BETWEEN 1 AND 31))");
            report.Add("Created HR_LEAVE_BALANCE_MIGRATION for idempotent cycle migration batches.");
        }
        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE'") == 0)
        {
            await Run("CREATE TABLE DEV_TEMPLATE.HR_LEAVE_BALANCE_MIGRATION_LINE (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY, MIGRATION_ID NUMBER(19) NOT NULL, SOURCE_BALANCE_ID NUMBER(19) NOT NULL, TARGET_BALANCE_ID NUMBER(19), EMPLOYEE_CODE NVARCHAR2(100) NOT NULL, LEAVE_TYPE_CODE NVARCHAR2(100) NOT NULL, SOURCE_CYCLE_START_DATE DATE NOT NULL, SOURCE_CYCLE_END_DATE DATE NOT NULL, SOURCE_YEAR_NO NUMBER(4) NOT NULL, SOURCE_START_WAS_NULL NUMBER(1) NOT NULL, SOURCE_END_WAS_NULL NUMBER(1) NOT NULL, SOURCE_ACCRUED_DAYS NUMBER(8,4) NOT NULL, SOURCE_USED_DAYS NUMBER(8,4) NOT NULL, SOURCE_CARRIED_DAYS NUMBER(8,4) NOT NULL, TARGET_CYCLE_START_DATE DATE NOT NULL, TARGET_CYCLE_END_DATE DATE NOT NULL, ALLOCATED_ACCRUED_DAYS NUMBER(8,4) NOT NULL, ALLOCATED_USED_DAYS NUMBER(8,4) NOT NULL, ALLOCATED_CARRIED_DAYS NUMBER(8,4) NOT NULL, TARGET_ACCRUED_BEFORE NUMBER(8,4) NOT NULL, TARGET_USED_BEFORE NUMBER(8,4) NOT NULL, TARGET_CARRIED_BEFORE NUMBER(8,4) NOT NULL, SOURCE_MOVEMENT_ID NUMBER(19), TARGET_MOVEMENT_ID NUMBER(19), CONSTRAINT PK_HR_BAL_MIGRATION_LINE PRIMARY KEY (ID), CONSTRAINT UX_HR_BAL_MIGRATION_LINE UNIQUE (MIGRATION_ID,SOURCE_BALANCE_ID,TARGET_CYCLE_START_DATE), CONSTRAINT FK_HR_BAL_MIG_LINE_BATCH FOREIGN KEY (MIGRATION_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_BALANCE_MIGRATION(ID), CONSTRAINT FK_HR_BAL_MIG_LINE_SRC FOREIGN KEY (SOURCE_BALANCE_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_BALANCE(ID), CONSTRAINT FK_HR_BAL_MIG_LINE_TGT FOREIGN KEY (TARGET_BALANCE_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_BALANCE(ID), CONSTRAINT FK_HR_BAL_MIG_LINE_SRCMOV FOREIGN KEY (SOURCE_MOVEMENT_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_MOVEMENT(ID), CONSTRAINT FK_HR_BAL_MIG_LINE_TGTMOV FOREIGN KEY (TARGET_MOVEMENT_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_MOVEMENT(ID), CONSTRAINT CK_HR_BAL_MIG_LINE_DATES CHECK (SOURCE_CYCLE_END_DATE>=SOURCE_CYCLE_START_DATE AND TARGET_CYCLE_END_DATE>=TARGET_CYCLE_START_DATE), CONSTRAINT CK_HR_BAL_MIG_LINE_SOURCE_NULLS CHECK (SOURCE_START_WAS_NULL IN (0,1) AND SOURCE_END_WAS_NULL IN (0,1)))");
            report.Add("Created HR_LEAVE_BALANCE_MIGRATION_LINE with allocation snapshots and restrictive balance/movement links.");
        }

        await HrSchemaColumnGuard.ValidateAsync(connection, MigrationColumns, allowMissingColumns: false);
        await Verify(connection, report);
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, MigrationColumns, allowMissingColumns: false);
        await ValidateIdentityColumns(connection, allowMissingTables: false);
        async Task<List<string[]>> Query(string sql)
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 20;
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<string[]>();
            while (await reader.ReadAsync())
                rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()!).ToArray());
            return rows;
        }

        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["HR_LEAVE_BALANCE_MIGRATION"] = ["ID","MIGRATION_KEY","EFFECTIVE_DATE","SOURCE_YEAR_START_MONTH","SOURCE_YEAR_START_DAY","TARGET_YEAR_START_MONTH","TARGET_YEAR_START_DAY","PAYLOAD_HASH","STATUS","CREATION_USER","CREATION_DATE","POSTED_BY","POSTED_DATE","REVERSED_BY","REVERSED_DATE"],
            ["HR_LEAVE_BALANCE_MIGRATION_LINE"] = ["ID","MIGRATION_ID","SOURCE_BALANCE_ID","TARGET_BALANCE_ID","EMPLOYEE_CODE","LEAVE_TYPE_CODE","SOURCE_CYCLE_START_DATE","SOURCE_CYCLE_END_DATE","SOURCE_YEAR_NO","SOURCE_START_WAS_NULL","SOURCE_END_WAS_NULL","SOURCE_ACCRUED_DAYS","SOURCE_USED_DAYS","SOURCE_CARRIED_DAYS","TARGET_CYCLE_START_DATE","TARGET_CYCLE_END_DATE","ALLOCATED_ACCRUED_DAYS","ALLOCATED_USED_DAYS","ALLOCATED_CARRIED_DAYS","TARGET_ACCRUED_BEFORE","TARGET_USED_BEFORE","TARGET_CARRIED_BEFORE","SOURCE_MOVEMENT_ID","TARGET_MOVEMENT_ID"]
        };
        foreach (var (table, columns) in expected)
        {
            var actual = await Query($"SELECT COLUMN_NAME FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' ORDER BY COLUMN_ID");
            if (!actual.Select(r => r[0]).SequenceEqual(columns, StringComparer.Ordinal))
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} columns are missing or have an unexpected shape.");
            await Query($"SELECT {string.Join(",", columns.Select(c => "\"" + c + "\""))} FROM DEV_TEMPLATE.\"{table}\" WHERE 1=0");
            report.Add($"{table}: {columns.Length} columns and Oracle mapped SELECT verified.");
        }
        await VerifyExistingStringDefinitions(connection);
        var constraints = await Query("SELECT CONSTRAINT_NAME,STATUS,VALIDATED FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME IN ('PK_HR_BAL_MIGRATION','UX_HR_BAL_MIGRATION_KEY','CK_HR_BAL_MIGRATION_STATUS','CK_HR_BAL_MIGRATION_MONTHS','CK_HR_BAL_MIGRATION_DAYS','PK_HR_BAL_MIGRATION_LINE','UX_HR_BAL_MIGRATION_LINE','FK_HR_BAL_MIG_LINE_BATCH','FK_HR_BAL_MIG_LINE_SRC','FK_HR_BAL_MIG_LINE_TGT','FK_HR_BAL_MIG_LINE_SRCMOV','FK_HR_BAL_MIG_LINE_TGTMOV','CK_HR_BAL_MIG_LINE_DATES','CK_HR_BAL_MIG_LINE_SOURCE_NULLS')");
        if (constraints.Count != 14 || constraints.Any(c => c[1] != "ENABLED" || c[2] != "VALIDATED"))
            throw new InvalidOperationException("Migration table constraints are missing, disabled or unvalidated.");
        report.Add("Migration idempotency, allocation uniqueness, date checks and restrictive foreign keys are enabled and validated.");
    }

    private static async Task VerifyExistingStringDefinitions(OracleConnection connection)
    {
        var expected = new (string Table, string Column, int Length, string Nullable)[]
        {
            ("HR_LEAVE_BALANCE_MIGRATION", "MIGRATION_KEY", 200, "N"),
            ("HR_LEAVE_BALANCE_MIGRATION", "PAYLOAD_HASH", 64, "N"),
            ("HR_LEAVE_BALANCE_MIGRATION", "STATUS", 20, "N"),
            ("HR_LEAVE_BALANCE_MIGRATION", "CREATION_USER", 200, "N"),
            ("HR_LEAVE_BALANCE_MIGRATION", "POSTED_BY", 200, "Y"),
            ("HR_LEAVE_BALANCE_MIGRATION", "REVERSED_BY", 200, "Y"),
            ("HR_LEAVE_BALANCE_MIGRATION_LINE", "EMPLOYEE_CODE", 100, "N"),
            ("HR_LEAVE_BALANCE_MIGRATION_LINE", "LEAVE_TYPE_CODE", 100, "N")
        };

        foreach (var item in expected)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName";
            command.Parameters.Add(new OracleParameter("tableName", item.Table));
            command.Parameters.Add(new OracleParameter("columnName", item.Column));
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) continue;
            if (reader.GetString(0) != "NVARCHAR2" || Convert.ToInt32(reader.GetValue(1)) != item.Length || reader.GetString(2) != item.Nullable)
                throw new InvalidOperationException($"DEV_TEMPLATE.{item.Table}.{item.Column} must be NVARCHAR2({item.Length}) with NULLABLE='{item.Nullable}'; no leave-balance migration DDL applied.");
        }
    }

    private static async Task PreflightConstraints(OracleConnection connection)
    {
        var existingTables = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in Constraints.Select(item => item.Table).Distinct(StringComparer.Ordinal))
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
            tableCommand.Parameters.Add(new OracleParameter("tableName", table));
            if (Convert.ToInt32(await tableCommand.ExecuteScalarAsync()) == 1) existingTables.Add(table);
        }

        foreach (var parentTable in Constraints.Where(item => item.Type == "R").Select(item => item.ParentTable!).Distinct(StringComparer.Ordinal))
        {
            if (!existingTables.Contains(parentTable))
            {
                if (Constraints.Any(item => item.Table == parentTable))
                    continue; // This installer creates the referenced migration header table below.
                await using var parentTableCommand = connection.CreateCommand();
                parentTableCommand.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
                parentTableCommand.Parameters.Add(new OracleParameter("tableName", parentTable));
                if (Convert.ToInt32(await parentTableCommand.ExecuteScalarAsync()) != 1)
                    throw new InvalidOperationException($"Required foreign-key parent DEV_TEMPLATE.{parentTable} is missing; no migration-table DDL applied.");
            }

            if (parentTable is "HR_LEAVE_BALANCE" or "HR_LEAVE_MOVEMENT")
            {
                await using (var parentId = connection.CreateCommand())
                {
                    parentId.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME='ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO'";
                    parentId.Parameters.Add(new OracleParameter("tableName", parentTable));
                    if (Convert.ToInt32(await parentId.ExecuteScalarAsync()) != 1)
                        throw new InvalidOperationException($"DEV_TEMPLATE.{parentTable}.ID must be a non-virtual NUMBER(19,0) NOT NULL column before leave migration DDL.");
                }

                await using var parentKey = connection.CreateCommand();
                parentKey.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS c WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME=:tableName AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED' AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1 AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='ID' AND cc.POSITION=1)";
                parentKey.Parameters.Add(new OracleParameter("tableName", parentTable));
                if (Convert.ToInt32(await parentKey.ExecuteScalarAsync()) == 0)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{parentTable}.ID lacks an enabled validated primary/unique key required by leave migration FKs; no migration-table DDL applied.");
            }
        }

        foreach (var expected in Constraints)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TABLE_NAME,CONSTRAINT_TYPE,STATUS,VALIDATED,SEARCH_CONDITION_VC,DELETE_RULE FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Add(new OracleParameter("constraintName", expected.Name));
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                if (existingTables.Contains(expected.Table))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{expected.Table} already exists but required constraint {expected.Name} is missing; installer cannot safely repair its constraint set.");
                continue;
            }

            var tableName = reader.GetString(0);
            var constraintType = reader.GetString(1);
            var status = reader.GetString(2);
            var validated = reader.GetString(3);
            var condition = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            var deleteRule = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            if (await reader.ReadAsync())
                throw new InvalidOperationException($"Constraint name {expected.Name} is not unique in DEV_TEMPLATE metadata.");
            if (tableName != expected.Table || constraintType != expected.Type || status != "ENABLED" || validated != "VALIDATED"
                || expected.Type == "R" && deleteRule != "NO ACTION")
                throw new InvalidOperationException($"Constraint {expected.Name} is attached to the wrong table/type or is disabled/unvalidated; no migration-table DDL applied.");

            if (expected.Type is "P" or "U" or "R")
            {
                await using var columns = connection.CreateCommand();
                columns.CommandText = "SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND CONSTRAINT_NAME=:constraintName ORDER BY POSITION";
                columns.Parameters.Add(new OracleParameter("tableName", expected.Table));
                columns.Parameters.Add(new OracleParameter("constraintName", expected.Name));
                await using var columnReader = await columns.ExecuteReaderAsync();
                var names = new List<string>();
                while (await columnReader.ReadAsync()) names.Add(columnReader.GetString(0));
                if (string.Join(",", names) != expected.Columns)
                    throw new InvalidOperationException($"Constraint {expected.Name} has an unexpected column signature; no migration-table DDL applied.");
            }

            if (expected.Type == "R")
            {
                await using var parent = connection.CreateCommand();
                parent.CommandText = "SELECT r.OWNER,r.TABLE_NAME,LISTAGG(rc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY rc.POSITION) FROM ALL_CONSTRAINTS c JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME=:constraintName GROUP BY r.OWNER,r.TABLE_NAME";
                parent.Parameters.Add(new OracleParameter("constraintName", expected.Name));
                await using var parentReader = await parent.ExecuteReaderAsync();
                if (!await parentReader.ReadAsync() || parentReader.GetString(0) != "DEV_TEMPLATE" || parentReader.GetString(1) != expected.ParentTable || parentReader.GetString(2) != expected.ParentColumns || await parentReader.ReadAsync())
                    throw new InvalidOperationException($"Foreign key {expected.Name} references an unexpected key; no migration-table DDL applied.");
            }

            if (expected.Type == "C" && NormalizeCondition(condition) != NormalizeCondition(expected.Condition ?? string.Empty))
                throw new InvalidOperationException($"Check constraint {expected.Name} has an unexpected condition; no migration-table DDL applied.");
        }
    }

    private static async Task ValidateIdentityColumns(OracleConnection connection, bool allowMissingTables)
    {
        foreach (var table in new[] { "HR_LEAVE_BALANCE_MIGRATION", "HR_LEAVE_BALANCE_MIGRATION_LINE" })
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
            tableCommand.Parameters.Add(new OracleParameter("tableName", table));
            if (Convert.ToInt32(await tableCommand.ExecuteScalarAsync()) == 0 && allowMissingTables) continue;

            await using var identityCommand = connection.CreateCommand();
            identityCommand.CommandText = "SELECT COUNT(*) FROM ALL_TAB_IDENTITY_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME='ID'";
            identityCommand.Parameters.Add(new OracleParameter("tableName", table));
            if (Convert.ToInt32(await identityCommand.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table}.ID must be an identity column; no migration-table DDL applied.");
        }
    }

    private static string NormalizeCondition(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '(' && c != ')' && c != '"').Select(char.ToUpperInvariant).ToArray());
}
