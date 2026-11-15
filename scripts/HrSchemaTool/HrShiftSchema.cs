using Oracle.ManagedDataAccess.Client;

internal static class HrShiftSchema
{
    private static readonly HrSchemaColumnExpectation[] AssignmentColumns =
    [
        new("HR_EMPLOYEE_SHIFT_ASSIGNMENT", "ASSIGNMENT_KIND", "NVARCHAR2", "Y", CharacterLength: 20, Additive: true)
    ];

    private static readonly HrSchemaColumnExpectation[] ShiftDayColumns =
    [
        new("HR_EMPLOYEE_SHIFT_DAY", "ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_EMPLOYEE_SHIFT_DAY", "EMPLOYEE_CODE", "NVARCHAR2", "N", CharacterLength: 100),
        new("HR_EMPLOYEE_SHIFT_DAY", "WORK_DATE", "DATE", "N"),
        new("HR_EMPLOYEE_SHIFT_DAY", "SHIFT_CODE", "NVARCHAR2", "Y", CharacterLength: 100),
        new("HR_EMPLOYEE_SHIFT_DAY", "IS_REST_DAY", "NUMBER", "N", Precision: 10, Scale: 0, DefaultExpression: "0"),
        new("HR_EMPLOYEE_SHIFT_DAY", "IS_ACTIVE", "NUMBER", "N", Precision: 10, Scale: 0, DefaultExpression: "1"),
        new("HR_EMPLOYEE_SHIFT_DAY", "SOURCE_REF", "NVARCHAR2", "N", CharacterLength: 20, DefaultExpression: "MANUAL"),
        new("HR_EMPLOYEE_SHIFT_DAY", "CREATION_USER", "NVARCHAR2", "N", CharacterLength: 200),
        new("HR_EMPLOYEE_SHIFT_DAY", "CREATION_DATE", "TIMESTAMP(7)", "N"),
        new("HR_EMPLOYEE_SHIFT_DAY", "UPDATE_USER", "NVARCHAR2", "Y", CharacterLength: 200),
        new("HR_EMPLOYEE_SHIFT_DAY", "UPDATE_DATE", "TIMESTAMP(7)", "Y")
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        var allColumns = AssignmentColumns.Concat(ShiftDayColumns).ToArray();
        await HrSchemaColumnGuard.ValidateAsync(connection, allColumns, allowMissingColumns: true,
            allowMissingTables: new HashSet<string>(["HR_EMPLOYEE_SHIFT_DAY"], StringComparer.Ordinal));
        await HrSchemaColumnGuard.ValidateAsync(connection,
            [new HrSchemaColumnExpectation("HR_EMPLOYEE_SHIFT_ASSIGNMENT", "EFFECTIVE_TO", "DATE", "Y")], allowMissingColumns: false);
        await PreflightAssignmentKindConstraint(connection);
        var shiftDayExists = await ScalarLong(connection,
            "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE_SHIFT_DAY'") == 1;
        if (!shiftDayExists)
            await PreflightShiftDayCreate(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE_SHIFT_ASSIGNMENT' AND COLUMN_NAME='ASSIGNMENT_KIND'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
        {
            command.CommandText = "ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_ASSIGNMENT ADD (ASSIGNMENT_KIND NVARCHAR2(20) NULL)";
            await command.ExecuteNonQueryAsync();
            report.Add("Added nullable ASSIGNMENT_KIND; existing assignments remain unclassified, without guessing historical base/temporary intent.");
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, AssignmentColumns, allowMissingColumns: false);
        command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE_SHIFT_ASSIGNMENT' AND CONSTRAINT_NAME='CK_HR_SHIFT_ASSIGN_KIND'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
        {
            command.CommandText = "ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_ASSIGNMENT ADD CONSTRAINT CK_HR_SHIFT_ASSIGN_KIND CHECK ((ASSIGNMENT_KIND IS NULL OR ASSIGNMENT_KIND IN ('BASE','TEMPORARY')) AND (ASSIGNMENT_KIND IS NULL OR ASSIGNMENT_KIND<>'TEMPORARY' OR EFFECTIVE_TO IS NOT NULL))";
            await command.ExecuteNonQueryAsync();
        }
        command.CommandText = "SELECT STATUS,VALIDATED FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE_SHIFT_ASSIGNMENT' AND CONSTRAINT_NAME='CK_HR_SHIFT_ASSIGN_KIND'";
        await using (var constraintState = await command.ExecuteReaderAsync())
        {
            if (!await constraintState.ReadAsync())
                throw new InvalidOperationException("CK_HR_SHIFT_ASSIGN_KIND was not present after installation.");
            if (constraintState.GetString(0) != "ENABLED" || constraintState.GetString(1) != "VALIDATED")
            {
                await constraintState.DisposeAsync();
                command.CommandText = "ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_ASSIGNMENT ENABLE VALIDATE CONSTRAINT CK_HR_SHIFT_ASSIGN_KIND";
                await command.ExecuteNonQueryAsync();
            }
        }
        if (shiftDayExists)
        {
            await HrSchemaColumnGuard.ValidateAsync(connection, ShiftDayColumns, allowMissingColumns: false);
            await ValidateShiftDayIdentity(connection);
            report.Add("HR_EMPLOYEE_SHIFT_DAY already exists; no data or DDL changed. Review preflight for compatibility.");
            return;
        }
        command.CommandText = """
            CREATE TABLE DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_DAY (
              ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY CONSTRAINT PK_HR_EMPLOYEE_SHIFT_DAY PRIMARY KEY,
              EMPLOYEE_CODE NVARCHAR2(100) NOT NULL, WORK_DATE DATE NOT NULL,
              SHIFT_CODE NVARCHAR2(100) NULL, IS_REST_DAY NUMBER(10) DEFAULT 0 NOT NULL,
              IS_ACTIVE NUMBER(10) DEFAULT 1 NOT NULL, SOURCE_REF NVARCHAR2(20) DEFAULT 'MANUAL' NOT NULL,
              CREATION_USER NVARCHAR2(200) NOT NULL, CREATION_DATE TIMESTAMP(7) NOT NULL,
              UPDATE_USER NVARCHAR2(200) NULL, UPDATE_DATE TIMESTAMP(7) NULL,
              CONSTRAINT UX_HR_SHIFT_DAY_EMP_DATE UNIQUE (EMPLOYEE_CODE,WORK_DATE),
              CONSTRAINT FK_HR_SHIFT_DAY_EMP FOREIGN KEY (EMPLOYEE_CODE) REFERENCES DEV_TEMPLATE.HR_EMPLOYEE(EMPLOYEE_CODE),
              CONSTRAINT FK_HR_SHIFT_DAY_SHIFT FOREIGN KEY (SHIFT_CODE) REFERENCES DEV_TEMPLATE.HR_SHIFT_SCHEDULE(SHIFT_CODE),
              CONSTRAINT CK_HR_SHIFT_DAY_DATE CHECK (WORK_DATE=TRUNC(WORK_DATE)),
              CONSTRAINT CK_HR_SHIFT_DAY_KIND CHECK ((IS_REST_DAY=1 AND SHIFT_CODE IS NULL) OR (IS_REST_DAY=0 AND SHIFT_CODE IS NOT NULL)),
              CONSTRAINT CK_HR_SHIFT_DAY_ACTIVE CHECK (IS_ACTIVE IN (0,1)),
              CONSTRAINT CK_HR_SHIFT_DAY_SOURCE CHECK (SOURCE_REF IN ('MANUAL','GENERATED')))
            """;
        await command.ExecuteNonQueryAsync();
        await HrSchemaColumnGuard.ValidateAsync(connection, ShiftDayColumns, allowMissingColumns: false);
        report.Add("Created DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_DAY with unique local work date, rest/source checks and employee/shift FKs. No schedules generated or existing assignments changed.");
    }

    private static async Task PreflightShiftDayCreate(OracleConnection connection)
    {
        foreach (var (table, column) in new[]
        {
            ("HR_EMPLOYEE", "EMPLOYEE_CODE"),
            ("HR_SHIFT_SCHEDULE", "SHIFT_CODE")
        })
        {
            await using (var parent = connection.CreateCommand())
            {
                parent.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
                parent.Parameters.Add(new OracleParameter("tableName", table));
                if (Convert.ToInt32(await parent.ExecuteScalarAsync()) != 1)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; shift-day installer stopped before DDL.");
            }

            await using (var metadata = connection.CreateCommand())
            {
                metadata.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName";
                metadata.Parameters.Add(new OracleParameter("tableName", table));
                metadata.Parameters.Add(new OracleParameter("columnName", column));
                await using var reader = await metadata.ExecuteReaderAsync();
                if (!await reader.ReadAsync() || !string.Equals(reader.GetString(0), "NVARCHAR2", StringComparison.OrdinalIgnoreCase) ||
                    Convert.ToInt32(reader.GetValue(1)) < 50 || !string.Equals(reader.GetString(2), "N", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(reader.GetString(3), "NO", StringComparison.OrdinalIgnoreCase) || await reader.ReadAsync())
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} must be a required, non-virtual NVARCHAR2(50) or wider column; shift-day installer stopped before DDL.");
            }

            await using (var key = connection.CreateCommand())
            {
                key.CommandText = """
                    SELECT COUNT(*) FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME=:tableName AND c.CONSTRAINT_TYPE IN ('P','U')
                      AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME=:columnName AND cc.POSITION=1)
                    """;
                key.Parameters.Add(new OracleParameter("tableName", table));
                key.Parameters.Add(new OracleParameter("columnName", column));
                if (Convert.ToInt32(await key.ExecuteScalarAsync()) == 0)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} has no enabled, validated single-column key; shift-day installer stopped before DDL.");
            }
        }

        foreach (var constraintName in new[]
        {
            "PK_HR_EMPLOYEE_SHIFT_DAY", "UX_HR_SHIFT_DAY_EMP_DATE", "FK_HR_SHIFT_DAY_EMP", "FK_HR_SHIFT_DAY_SHIFT",
            "CK_HR_SHIFT_DAY_DATE", "CK_HR_SHIFT_DAY_KIND", "CK_HR_SHIFT_DAY_ACTIVE", "CK_HR_SHIFT_DAY_SOURCE"
        })
        {
            await using var constraint = connection.CreateCommand();
            constraint.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
            constraint.Parameters.Add(new OracleParameter("constraintName", constraintName));
            if (Convert.ToInt32(await constraint.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException($"Constraint name {constraintName} is already used in DEV_TEMPLATE; shift-day installer stopped before DDL.");
        }

        await using var index = connection.CreateCommand();
        index.CommandText = "SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_SHIFT_DAY_EMP_DATE'";
        if (Convert.ToInt32(await index.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Index name UX_HR_SHIFT_DAY_EMP_DATE is already used in DEV_TEMPLATE; shift-day installer stopped before DDL.");
    }

    private static async Task<long> ScalarLong(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 15;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ValidateShiftDayIdentity(OracleConnection connection)
    {
        var identities = await ScalarLong(connection,
            "SELECT COUNT(*) FROM ALL_TAB_IDENTITY_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE_SHIFT_DAY' AND COLUMN_NAME='ID'");
        if (identities != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_DAY.ID must be an Oracle identity column; shift installer made no changes.");
    }

    private static async Task PreflightAssignmentKindConstraint(OracleConnection connection)
    {
        const string name = "CK_HR_SHIFT_ASSIGN_KIND";
        const string expectedCondition = "(ASSIGNMENT_KIND IS NULL OR ASSIGNMENT_KIND IN ('BASE','TEMPORARY')) AND (ASSIGNMENT_KIND IS NULL OR ASSIGNMENT_KIND<>'TEMPORARY' OR EFFECTIVE_TO IS NOT NULL)";
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT TABLE_NAME,CONSTRAINT_TYPE,STATUS,VALIDATED,SEARCH_CONDITION_VC FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Add(new OracleParameter("constraintName", name));
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var table = reader.GetString(0);
                var type = reader.GetString(1);
                var status = reader.GetString(2);
                var validated = reader.GetString(3);
                var condition = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                static string Normalize(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '\"').Select(char.ToUpperInvariant).ToArray());
                if (table != "HR_EMPLOYEE_SHIFT_ASSIGNMENT" || type != "C" ||
                    Normalize(condition) != Normalize(expectedCondition))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{name} is attached to a different object or has a different condition; shift repair stopped before DDL.");
                if (status is not ("ENABLED" or "DISABLED") || validated is not ("VALIDATED" or "NOT VALIDATED"))
                    throw new InvalidOperationException($"DEV_TEMPLATE.{name} has unsupported constraint metadata; shift repair stopped before DDL.");
                if (await reader.ReadAsync())
                    throw new InvalidOperationException($"DEV_TEMPLATE.{name} is ambiguous; shift repair stopped before DDL.");
            }
        }

        await using var metadata = connection.CreateCommand();
        metadata.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE_SHIFT_ASSIGNMENT' AND COLUMN_NAME='ASSIGNMENT_KIND'";
        var assignmentKindExists = Convert.ToInt32(await metadata.ExecuteScalarAsync()) == 1;
        if (!assignmentKindExists) return; // The new nullable column will contain only NULL values.

        await using var data = connection.CreateCommand();
        data.CommandText = "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_ASSIGNMENT WHERE ASSIGNMENT_KIND IS NOT NULL AND (ASSIGNMENT_KIND NOT IN ('BASE','TEMPORARY') OR (ASSIGNMENT_KIND='TEMPORARY' AND EFFECTIVE_TO IS NULL))";
        if (Convert.ToInt64(await data.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException($"DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_ASSIGNMENT contains rows that violate {name}; shift repair stopped before DDL.");
    }
}
