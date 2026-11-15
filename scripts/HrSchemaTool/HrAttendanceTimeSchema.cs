using Oracle.ManagedDataAccess.Client;

internal static class HrAttendanceTimeSchema
{
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_ATTENDANCE_TIME_SETTINGS", "ID", "NUMBER", "N", Precision: 1, Scale: 0),
        new("HR_ATTENDANCE_TIME_SETTINGS", "TIME_ZONE_ID", "NVARCHAR2", "N", CharacterLength: 100),
        new("HR_ATTENDANCE_TIME_SETTINGS", "CREATION_USER", "NVARCHAR2", "N", CharacterLength: 200),
        new("HR_ATTENDANCE_TIME_SETTINGS", "CREATION_DATE", "TIMESTAMP(7)", "N"),
        new("HR_ATTENDANCE_DAY", "TIME_ZONE_ID", "NVARCHAR2", "Y", CharacterLength: 100, Additive: true),
        new("HR_ATTENDANCE_DAY", "SHIFT_STARTS_AT_UTC", "TIMESTAMP(7)", "Y", Additive: true),
        new("HR_ATTENDANCE_DAY", "SHIFT_ENDS_AT_UTC", "TIMESTAMP(7)", "Y", Additive: true),
        new("HR_ATTENDANCE_DAY", "SHIFT_BREAK_MINUTES", "NUMBER", "Y", Precision: 10, Scale: 0, Additive: true),
        new("HR_ATTENDANCE_DAY", "SHIFT_SOURCE", "NVARCHAR2", "Y", CharacterLength: 30, Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: true,
            allowMissingTables: new HashSet<string>(["HR_ATTENDANCE_TIME_SETTINGS"], StringComparer.Ordinal));
        await PreflightSchemaKey(connection);
        await PreflightImmutableTrigger(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
        {
            command.CommandText = """
                CREATE TABLE DEV_TEMPLATE.HR_ATTENDANCE_TIME_SETTINGS (
                  ID NUMBER(1) NOT NULL CONSTRAINT PK_HR_ATT_TIME_SETTINGS PRIMARY KEY,
                  TIME_ZONE_ID NVARCHAR2(100) NOT NULL,
                  CREATION_USER NVARCHAR2(200) NOT NULL,
                  CREATION_DATE TIMESTAMP(7) NOT NULL
                )
                """;
            await command.ExecuteNonQueryAsync();
        }
        await EnsureSchemaKey(connection);
        command.CommandText = """
            CREATE OR REPLACE TRIGGER DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE
            BEFORE UPDATE OR DELETE ON DEV_TEMPLATE.HR_ATTENDANCE_TIME_SETTINGS
            BEGIN
              RAISE_APPLICATION_ERROR(-20072, 'Attendance time settings require a dated migration; UPDATE and DELETE are disabled.');
            END;
            """;
        await command.ExecuteNonQueryAsync();
        foreach (var column in new[] { ("TIME_ZONE_ID", "NVARCHAR2(100)"), ("SHIFT_STARTS_AT_UTC", "TIMESTAMP(7)"), ("SHIFT_ENDS_AT_UTC", "TIMESTAMP(7)"), ("SHIFT_BREAK_MINUTES", "NUMBER(10)"), ("SHIFT_SOURCE", "NVARCHAR2(30)") })
        {
            command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_DAY' AND COLUMN_NAME=:columnName";
            command.Parameters.Clear(); command.Parameters.Add(new OracleParameter("columnName", column.Item1));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) continue;
            command.Parameters.Clear(); command.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_ATTENDANCE_DAY ADD ({column.Item1} {column.Item2} NULL)";
            await command.ExecuteNonQueryAsync(); report.Add("Added nullable attendance snapshot column: " + column.Item1);
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        report.Add("Tenant schema attendance time settings table and immutable trigger installed. No zone is selected automatically.");
    }

    private static async Task PreflightImmutableTrigger(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_OWNER,TABLE_NAME,TRIGGER_TYPE,TRIGGERING_EVENT FROM ALL_TRIGGERS WHERE OWNER='DEV_TEMPLATE' AND TRIGGER_NAME='TRG_HR_TIME_IMMUTABLE'";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return;
        var tableOwner = reader.GetString(0);
        var tableName = reader.GetString(1);
        var triggerType = reader.GetString(2);
        var events = reader.GetString(3);
        if (await reader.ReadAsync())
            throw new InvalidOperationException("Multiple DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE trigger metadata rows were found; attendance time installer stopped before DDL.");
        if (tableOwner != "DEV_TEMPLATE" || tableName != "HR_ATTENDANCE_TIME_SETTINGS" || triggerType != "BEFORE STATEMENT" ||
            !events.Contains("UPDATE", StringComparison.Ordinal) || !events.Contains("DELETE", StringComparison.Ordinal))
            throw new InvalidOperationException("DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE is attached to a different table or event; replacement was refused before DDL.");

        await reader.DisposeAsync();
        await HrTriggerDefinitionGuard.RequireBodyAsync(connection, "TRG_HR_TIME_IMMUTABLE", """
            BEGIN
              RAISE_APPLICATION_ERROR(-20072, 'Attendance time settings require a dated migration; UPDATE and DELETE are disabled.');
            END;
            """);
    }

    private static async Task PreflightSchemaKey(OracleConnection connection)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS'") == 0)
            return;

        string? keyName = null;
        string? status = null;
        string? validated = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT CONSTRAINT_NAME,STATUS,VALIDATED FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS' AND CONSTRAINT_TYPE='P'";
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                keyName = reader.GetString(0); status = reader.GetString(1); validated = reader.GetString(2);
                if (await reader.ReadAsync())
                    throw new InvalidOperationException("DEV_TEMPLATE.HR_ATTENDANCE_TIME_SETTINGS has ambiguous primary-key metadata; attendance time installer stopped before DDL.");
            }
        }

        if (keyName is not null)
        {
            await using var columns = connection.CreateCommand();
            columns.CommandText = "SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS' AND CONSTRAINT_NAME=:constraintName ORDER BY POSITION";
            columns.Parameters.Add(new OracleParameter("constraintName", keyName));
            var actual = new List<string>();
            await using (var reader = await columns.ExecuteReaderAsync())
                while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
            if (!actual.SequenceEqual(["ID"], StringComparer.Ordinal))
                throw new InvalidOperationException("DEV_TEMPLATE.HR_ATTENDANCE_TIME_SETTINGS requires the schema-scoped ID primary key; apply HR migration 20261115002400 before installing.");
        }
        else
        {
            if (await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='PK_HR_ATT_TIME_SETTINGS'") != 0 ||
                await Count("SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='PK_HR_ATT_TIME_SETTINGS'") != 0)
                throw new InvalidOperationException("DEV_TEMPLATE.PK_HR_ATT_TIME_SETTINGS is reserved by another constraint or index; attendance time installer stopped before DDL.");
        }

        if (await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_ATTENDANCE_TIME_SETTINGS WHERE ID<>1") != 0)
            throw new InvalidOperationException("HR_ATTENDANCE_TIME_SETTINGS must contain only singleton ID=1; attendance time installer stopped before DDL.");
        if (keyName is not null && (status is not ("ENABLED" or "DISABLED") || validated is not ("VALIDATED" or "NOT VALIDATED")))
            throw new InvalidOperationException("HR_ATTENDANCE_TIME_SETTINGS primary key has unsupported state; attendance time installer stopped before DDL.");
    }

    private static async Task EnsureSchemaKey(OracleConnection connection)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        if (await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS' AND CONSTRAINT_TYPE='P'") == 0)
        {
            await using var add = connection.CreateCommand();
            add.CommandText = "ALTER TABLE DEV_TEMPLATE.HR_ATTENDANCE_TIME_SETTINGS ADD CONSTRAINT PK_HR_ATT_TIME_SETTINGS PRIMARY KEY (ID)";
            await add.ExecuteNonQueryAsync();
        }
        await using var state = connection.CreateCommand();
        state.CommandText = "SELECT CONSTRAINT_NAME,STATUS,VALIDATED FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS' AND CONSTRAINT_TYPE='P'";
        await using var reader = await state.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("HR_ATTENDANCE_TIME_SETTINGS primary key is missing after installation.");
        var constraintName = reader.GetString(0);
        if (reader.GetString(1) != "ENABLED" || reader.GetString(2) != "VALIDATED")
        {
            await reader.DisposeAsync();
            await using var enable = connection.CreateCommand();
            enable.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_ATTENDANCE_TIME_SETTINGS ENABLE VALIDATE CONSTRAINT \"{constraintName.Replace("\"", "\"\"")}\"";
            await enable.ExecuteNonQueryAsync();
        }
    }
}
