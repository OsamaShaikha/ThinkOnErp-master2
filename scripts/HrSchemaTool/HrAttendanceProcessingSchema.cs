using Oracle.ManagedDataAccess.Client;

internal static class HrAttendanceProcessingSchema
{
    private static readonly HrSchemaColumnExpectation[] BaseColumns =
    [
        new("HR_RAW_ATTENDANCE", "ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_RAW_ATTENDANCE", "EMPLOYEE_CODE", "NVARCHAR2", "N", CharacterLength: 50),
        new("HR_RAW_ATTENDANCE", "IS_PROCESSED", "NUMBER", "N", Precision: 1, Scale: 0),
        new("HR_RAW_ATTENDANCE", "PUNCH_TIME", "TIMESTAMP(7)", "N"),
        new("HR_RAW_ATTENDANCE", "EVENT_ID", "NVARCHAR2", "Y", CharacterLength: 100),
        new("HR_RAW_ATTENDANCE", "PROCESSED_DATE", "TIMESTAMP(7)", "Y"),
        new("HR_ATTENDANCE_DAY", "ID", "NUMBER", "N", Precision: 19, Scale: 0)
    ];

    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_RAW_ATTENDANCE", "RESOLVED_WORK_DATE", "DATE", "Y", Additive: true),
        new("HR_RAW_ATTENDANCE", "ATTENDANCE_DAY_ID", "NUMBER", "Y", Precision: 19, Scale: 0, Additive: true),
        new("HR_RAW_ATTENDANCE", "PROCESSING_OUTCOME", "NVARCHAR2", "Y", CharacterLength: 20, Additive: true),
        new("HR_RAW_ATTENDANCE", "PROCESSING_REASON", "NVARCHAR2", "Y", CharacterLength: 1000, Additive: true),
        new("HR_RAW_ATTENDANCE", "PROCESSED_BY", "NVARCHAR2", "Y", CharacterLength: 200, Additive: true),
        new("HR_RAW_ATTENDANCE", "LAST_ATTEMPTED_AT_UTC", "TIMESTAMP(7)", "Y", Additive: true),
        new("HR_RAW_ATTENDANCE", "LAST_ATTEMPTED_BY", "NVARCHAR2", "Y", CharacterLength: 200, Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, BaseColumns, allowMissingColumns: false);
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: true);
        await PreflightConstraintsAndIndexes(connection);
        await using var command = connection.CreateCommand();
        foreach (var column in new[] { ("RESOLVED_WORK_DATE", "DATE"), ("ATTENDANCE_DAY_ID", "NUMBER(19)"), ("PROCESSING_OUTCOME", "NVARCHAR2(20)"),
                     ("PROCESSING_REASON", "NVARCHAR2(1000)"), ("PROCESSED_BY", "NVARCHAR2(200)"), ("LAST_ATTEMPTED_AT_UTC", "TIMESTAMP(7)"), ("LAST_ATTEMPTED_BY", "NVARCHAR2(200)") })
        {
            command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND COLUMN_NAME=:columnName";
            command.Parameters.Clear(); command.Parameters.Add(new OracleParameter("columnName", column.Item1));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) continue;
            command.Parameters.Clear(); command.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_RAW_ATTENDANCE ADD ({column.Item1} {column.Item2} NULL)";
            await command.ExecuteNonQueryAsync(); report.Add("Added nullable raw processing column: " + column.Item1);
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        command.Parameters.Clear(); command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='FK_HR_RAW_DAY'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
        {
            command.CommandText = "ALTER TABLE DEV_TEMPLATE.HR_RAW_ATTENDANCE ADD CONSTRAINT FK_HR_RAW_DAY FOREIGN KEY (ATTENDANCE_DAY_ID) REFERENCES DEV_TEMPLATE.HR_ATTENDANCE_DAY(ID)";
            await command.ExecuteNonQueryAsync();
        }
        command.CommandText = "SELECT STATUS,VALIDATED FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND CONSTRAINT_NAME='FK_HR_RAW_DAY'";
        await using (var fkState = await command.ExecuteReaderAsync())
        {
            if (!await fkState.ReadAsync()) throw new InvalidOperationException("FK_HR_RAW_DAY is missing after installation.");
            if (fkState.GetString(0) != "ENABLED" || fkState.GetString(1) != "VALIDATED")
            {
                await fkState.DisposeAsync();
                command.CommandText = "ALTER TABLE DEV_TEMPLATE.HR_RAW_ATTENDANCE ENABLE VALIDATE CONSTRAINT FK_HR_RAW_DAY";
                await command.ExecuteNonQueryAsync();
            }
        }
        command.CommandText = "SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='IX_HR_RAW_WORK_DATE'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
        {
            command.CommandText = "CREATE INDEX DEV_TEMPLATE.IX_HR_RAW_WORK_DATE ON DEV_TEMPLATE.HR_RAW_ATTENDANCE(EMPLOYEE_CODE,RESOLVED_WORK_DATE)";
            await command.ExecuteNonQueryAsync();
        }
        foreach (var index in new[] { ("IX_HR_RAW_DAY", "ATTENDANCE_DAY_ID"), ("IX_HR_RAW_QUEUE", "IS_PROCESSED,PUNCH_TIME,ID"), ("IX_HR_RAW_EVENT_LOOKUP", "EVENT_ID,EMPLOYEE_CODE") })
        {
            command.Parameters.Clear(); command.CommandText = "SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME=:indexName";
            command.Parameters.Add(new OracleParameter("indexName", index.Item1));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) continue;
            command.Parameters.Clear(); command.CommandText = $"CREATE INDEX DEV_TEMPLATE.{index.Item1} ON DEV_TEMPLATE.HR_RAW_ATTENDANCE({index.Item2})";
            await command.ExecuteNonQueryAsync();
        }
        foreach (var check in new[] {
            ("CK_HR_RAW_WORK_DATE", "RESOLVED_WORK_DATE IS NULL OR RESOLVED_WORK_DATE=TRUNC(RESOLVED_WORK_DATE)"),
            ("CK_HR_RAW_OUTCOME", "PROCESSING_OUTCOME IS NULL OR (IS_PROCESSED=1 AND PROCESSED_BY IS NOT NULL AND PROCESSED_DATE IS NOT NULL AND ((PROCESSING_OUTCOME='APPLIED' AND RESOLVED_WORK_DATE IS NOT NULL AND ATTENDANCE_DAY_ID IS NOT NULL) OR (PROCESSING_OUTCOME IN ('IGNORED','REJECTED') AND PROCESSING_REASON IS NOT NULL)))") })
        {
            command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Clear(); command.Parameters.Add(new OracleParameter("constraintName", check.Item1));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
            {
                command.Parameters.Clear();
                command.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_RAW_ATTENDANCE ADD CONSTRAINT {check.Item1} CHECK ({check.Item2})";
                await command.ExecuteNonQueryAsync();
            }
            command.Parameters.Clear(); command.CommandText = "SELECT STATUS,VALIDATED FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Add(new OracleParameter("constraintName", check.Item1));
            await using (var checkState = await command.ExecuteReaderAsync())
            {
                if (!await checkState.ReadAsync()) throw new InvalidOperationException($"{check.Item1} is missing after installation.");
                if (checkState.GetString(0) != "ENABLED" || checkState.GetString(1) != "VALIDATED")
                {
                    await checkState.DisposeAsync();
                    command.Parameters.Clear(); command.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_RAW_ATTENDANCE ENABLE VALIDATE CONSTRAINT {check.Item1}";
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        report.Add("Raw processing link/index installed. No old evidence linked, processed, ignored or reinterpreted.");
    }

    private static async Task PreflightConstraintsAndIndexes(OracleConnection connection)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        async Task<bool> IsEnabledValidatedParentKey(string constraintName)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_DAY' AND CONSTRAINT_NAME=:constraintName AND CONSTRAINT_TYPE IN ('P','U') AND STATUS='ENABLED' AND VALIDATED='VALIDATED'";
            command.Parameters.Add(new OracleParameter("constraintName", constraintName));
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        async Task<string[]> IndexColumns(string name)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COLUMN_NAME FROM ALL_IND_COLUMNS WHERE INDEX_OWNER='DEV_TEMPLATE' AND INDEX_NAME=:indexName ORDER BY COLUMN_POSITION";
            command.Parameters.Add(new OracleParameter("indexName", name));
            var columns = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            return columns.ToArray();
        }
        async Task<string[]> ConstraintColumns(string owner, string table, string name)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS WHERE OWNER=:owner AND TABLE_NAME=:tableName AND CONSTRAINT_NAME=:constraintName ORDER BY POSITION";
            command.Parameters.Add(new OracleParameter("owner", owner));
            command.Parameters.Add(new OracleParameter("tableName", table));
            command.Parameters.Add(new OracleParameter("constraintName", name));
            var columns = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            return columns.ToArray();
        }

        foreach (var column in new[] { "ID", "EMPLOYEE_CODE", "IS_PROCESSED", "PUNCH_TIME", "EVENT_ID", "PROCESSED_DATE" })
            if (await Count($"SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND COLUMN_NAME='{column}'") != 1)
                throw new InvalidOperationException($"DEV_TEMPLATE.HR_RAW_ATTENDANCE.{column} is missing; raw-processing repair stopped before DDL.");

        var parentExists = await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_DAY'") == 1;
        if (!parentExists)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_ATTENDANCE_DAY is missing; raw-processing repair stopped before DDL.");
        if (await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS c WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_ATTENDANCE_DAY' AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED' AND c.CONSTRAINT_NAME IN (SELECT CONSTRAINT_NAME FROM ALL_CONS_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_DAY' AND COLUMN_NAME='ID' GROUP BY CONSTRAINT_NAME HAVING COUNT(*)=1)") == 0)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_ATTENDANCE_DAY.ID has no enabled, validated single-column primary/unique key; raw-processing repair stopped before DDL.");

        var indexPlans = new[]
        {
            (Name:"IX_HR_RAW_WORK_DATE", Columns:new[] { "EMPLOYEE_CODE", "RESOLVED_WORK_DATE" }),
            (Name:"IX_HR_RAW_DAY", Columns:new[] { "ATTENDANCE_DAY_ID" }),
            (Name:"IX_HR_RAW_QUEUE", Columns:new[] { "IS_PROCESSED", "PUNCH_TIME", "ID" }),
            (Name:"IX_HR_RAW_EVENT_LOOKUP", Columns:new[] { "EVENT_ID", "EMPLOYEE_CODE" })
        };
        foreach (var plan in indexPlans)
        {
            string? table = null;
            string? uniqueness = null;
            string? status = null;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT TABLE_NAME,UNIQUENESS,STATUS FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME=:indexName";
                command.Parameters.Add(new OracleParameter("indexName", plan.Name));
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    table = reader.GetString(0); uniqueness = reader.GetString(1); status = reader.GetString(2);
                    if (await reader.ReadAsync()) throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Name} has ambiguous metadata; raw-processing repair stopped before DDL.");
                }
            }
            if (table is null) continue;
            if (table != "HR_RAW_ATTENDANCE" || uniqueness != "NONUNIQUE" || status != "VALID" ||
                !(await IndexColumns(plan.Name)).SequenceEqual(plan.Columns, StringComparer.Ordinal))
                throw new InvalidOperationException($"DEV_TEMPLATE.{plan.Name} is attached to another object or has incompatible columns/state; raw-processing repair stopped before DDL.");
        }

        const string expectedFkName = "FK_HR_RAW_DAY";
        string? fkTable = null; string? fkType = null; string? fkOwner = null; string? fkRefName = null; string? fkDeleteRule = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT TABLE_NAME,CONSTRAINT_TYPE,R_OWNER,R_CONSTRAINT_NAME,DELETE_RULE FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Add(new OracleParameter("constraintName", expectedFkName));
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                fkTable = reader.GetString(0); fkType = reader.GetString(1); fkOwner = reader.IsDBNull(2) ? null : reader.GetString(2);
                fkRefName = reader.IsDBNull(3) ? null : reader.GetString(3); fkDeleteRule = reader.GetString(4);
                if (await reader.ReadAsync()) throw new InvalidOperationException($"DEV_TEMPLATE.{expectedFkName} is ambiguous; raw-processing repair stopped before DDL.");
            }
        }
        if (fkTable is not null)
        {
            if (fkTable != "HR_RAW_ATTENDANCE" || fkType != "R" || fkOwner != "DEV_TEMPLATE" || string.IsNullOrEmpty(fkRefName) || fkDeleteRule != "NO ACTION" ||
                !(await ConstraintColumns("DEV_TEMPLATE", "HR_RAW_ATTENDANCE", expectedFkName)).SequenceEqual(["ATTENDANCE_DAY_ID"], StringComparer.Ordinal) ||
                !await IsEnabledValidatedParentKey(fkRefName) ||
                !(await ConstraintColumns("DEV_TEMPLATE", "HR_ATTENDANCE_DAY", fkRefName)).SequenceEqual(["ID"], StringComparer.Ordinal))
                throw new InvalidOperationException($"DEV_TEMPLATE.{expectedFkName} has incompatible target metadata; raw-processing repair stopped before DDL.");
        }

        if (await Count("SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND COLUMN_NAME='ATTENDANCE_DAY_ID'") != 0 &&
            await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_RAW_ATTENDANCE r WHERE r.ATTENDANCE_DAY_ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.HR_ATTENDANCE_DAY d WHERE d.ID=r.ATTENDANCE_DAY_ID)") != 0)
            throw new InvalidOperationException("HR_RAW_ATTENDANCE contains orphan ATTENDANCE_DAY_ID values; raw-processing repair stopped before DDL.");

        var checks = new[]
        {
            (Name:"CK_HR_RAW_WORK_DATE", Condition:"RESOLVED_WORK_DATE IS NULL OR RESOLVED_WORK_DATE=TRUNC(RESOLVED_WORK_DATE)", Invalid:"RESOLVED_WORK_DATE IS NOT NULL AND RESOLVED_WORK_DATE<>TRUNC(RESOLVED_WORK_DATE)", Columns:new[] { "RESOLVED_WORK_DATE" }),
            (Name:"CK_HR_RAW_OUTCOME", Condition:"PROCESSING_OUTCOME IS NULL OR (IS_PROCESSED=1 AND PROCESSED_BY IS NOT NULL AND PROCESSED_DATE IS NOT NULL AND ((PROCESSING_OUTCOME='APPLIED' AND RESOLVED_WORK_DATE IS NOT NULL AND ATTENDANCE_DAY_ID IS NOT NULL) OR (PROCESSING_OUTCOME IN ('IGNORED','REJECTED') AND PROCESSING_REASON IS NOT NULL)))", Invalid:"PROCESSING_OUTCOME IS NOT NULL AND (NVL(IS_PROCESSED,0)<>1 OR PROCESSED_BY IS NULL OR PROCESSED_DATE IS NULL OR NOT ((PROCESSING_OUTCOME='APPLIED' AND RESOLVED_WORK_DATE IS NOT NULL AND ATTENDANCE_DAY_ID IS NOT NULL) OR (PROCESSING_OUTCOME IN ('IGNORED','REJECTED') AND PROCESSING_REASON IS NOT NULL)))", Columns:new[] { "PROCESSING_OUTCOME", "IS_PROCESSED", "PROCESSED_BY", "PROCESSED_DATE", "RESOLVED_WORK_DATE", "ATTENDANCE_DAY_ID", "PROCESSING_REASON" })
        };
        foreach (var check in checks)
        {
            string? table = null; string? type = null; string? condition = null;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT TABLE_NAME,CONSTRAINT_TYPE,SEARCH_CONDITION_VC FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
                command.Parameters.Add(new OracleParameter("constraintName", check.Name));
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    table = reader.GetString(0); type = reader.GetString(1); condition = reader.IsDBNull(2) ? null : reader.GetString(2);
                    if (await reader.ReadAsync()) throw new InvalidOperationException($"DEV_TEMPLATE.{check.Name} is ambiguous; raw-processing repair stopped before DDL.");
                }
            }
            static string Normalize(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '\"').Select(char.ToUpperInvariant).ToArray());
            if (table is not null && (table != "HR_RAW_ATTENDANCE" || type != "C" || Normalize(condition ?? string.Empty) != Normalize(check.Condition)))
                throw new InvalidOperationException($"DEV_TEMPLATE.{check.Name} has an incompatible definition; raw-processing repair stopped before DDL.");

            var existingCheckColumns = new List<string>();
            foreach (var column in check.Columns)
                if (await Count($"SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND COLUMN_NAME='{column}'") == 1)
                    existingCheckColumns.Add(column);
            if (check.Name == "CK_HR_RAW_OUTCOME" && !existingCheckColumns.Contains("PROCESSING_OUTCOME")) continue;
            if (existingCheckColumns.Count != check.Columns.Length)
            {
                if (check.Name == "CK_HR_RAW_OUTCOME" && await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_RAW_ATTENDANCE WHERE PROCESSING_OUTCOME IS NOT NULL") != 0)
                    throw new InvalidOperationException("HR_RAW_ATTENDANCE has outcome values but lacks columns needed to validate their check; raw-processing repair stopped before DDL.");
                continue;
            }
            if (await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.HR_RAW_ATTENDANCE WHERE {check.Invalid}") != 0)
                throw new InvalidOperationException($"HR_RAW_ATTENDANCE contains rows that violate {check.Name}; raw-processing repair stopped before DDL.");
        }
    }
}
