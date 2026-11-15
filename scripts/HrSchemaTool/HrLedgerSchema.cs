using Oracle.ManagedDataAccess.Client;

internal static class HrLedgerSchema
{
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_LEAVE_MOVEMENT", "ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_LEAVE_MOVEMENT", "BALANCE_ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_LEAVE_MOVEMENT", "EMPLOYEE_CODE", "NVARCHAR2", "N", CharacterLength: 100),
        new("HR_LEAVE_MOVEMENT", "LEAVE_TYPE_CODE", "NVARCHAR2", "N", CharacterLength: 100),
        new("HR_LEAVE_MOVEMENT", "CYCLE_START_DATE", "TIMESTAMP(7)", "N"),
        new("HR_LEAVE_MOVEMENT", "SEQUENCE_NO", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_LEAVE_MOVEMENT", "KIND", "NVARCHAR2", "N", CharacterLength: 40),
        new("HR_LEAVE_MOVEMENT", "OPERATION_KEY", "NVARCHAR2", "N", CharacterLength: 64),
        new("HR_LEAVE_MOVEMENT", "PAYLOAD_HASH", "NVARCHAR2", "Y", CharacterLength: 64),
        new("HR_LEAVE_MOVEMENT", "SOURCE_REF", "NVARCHAR2", "N", CharacterLength: 300),
        new("HR_LEAVE_MOVEMENT", "REASON", "NVARCHAR2", "N", CharacterLength: 1000),
        new("HR_LEAVE_MOVEMENT", "ACTOR", "NVARCHAR2", "N", CharacterLength: 200),
        new("HR_LEAVE_MOVEMENT", "CREATED_DATE", "TIMESTAMP(7)", "N"),
        new("HR_LEAVE_MOVEMENT", "ACCRUED_DELTA", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "USED_DELTA", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "CARRIED_DELTA", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "ACCRUED_AFTER", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "USED_AFTER", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "CARRIED_AFTER", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "EXPIRED_DAYS", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "TRANSFERRED_DAYS", "NUMBER", "N", Precision: 12, Scale: 4),
        new("HR_LEAVE_MOVEMENT", "REVERSES_MOVEMENT_ID", "NUMBER", "Y", Precision: 19, Scale: 0)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false,
            allowMissingTables: new HashSet<string>(["HR_LEAVE_MOVEMENT"], StringComparer.Ordinal));
        await ValidateHistoryTriggerTarget(connection, report);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM ALL_TAB_COLS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_LEAVE_BALANCE' AND c.COLUMN_NAME='ID'
              AND c.DATA_TYPE='NUMBER' AND c.DATA_PRECISION=19 AND c.DATA_SCALE=0
              AND c.NULLABLE='N' AND c.VIRTUAL_COLUMN='NO'
              AND EXISTS (
                  SELECT 1 FROM ALL_CONSTRAINTS p
                  WHERE p.OWNER=c.OWNER AND p.TABLE_NAME=c.TABLE_NAME
                    AND p.CONSTRAINT_TYPE IN ('P','U') AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED'
                    AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1
                    AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME AND pc.COLUMN_NAME='ID' AND pc.POSITION=1)
              )
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("HR_LEAVE_BALANCE.ID must be NUMBER(19,0) NOT NULL with an enabled, validated single-column key before HR_LEAVE_MOVEMENT is installed.");

        command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_MOVEMENT'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
        {
            report.Add("HR_LEAVE_MOVEMENT already exists; refreshing append-only protection and checking mappings.");
            await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
            await ValidateLedgerKeysAsync(connection);
            await ProtectHistory(connection, report);
            return;
        }
        command.CommandText = "SELECT (SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME IN ('PK_HR_LEAVE_MOVEMENT','FK_HR_LEAVE_MOVEMENT_BAL','FK_HR_LEAVE_MOVEMENT_REV','UX_HR_LEAVE_MOVEMENT_OP','UX_HR_LEAVE_MOVEMENT_SEQ','UX_HR_LEAVE_MOVEMENT_REV')) + (SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME IN ('PK_HR_LEAVE_MOVEMENT','UX_HR_LEAVE_MOVEMENT_OP','UX_HR_LEAVE_MOVEMENT_SEQ','UX_HR_LEAVE_MOVEMENT_REV')) FROM DUAL";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("HR_LEAVE_MOVEMENT constraint or index names are already in use before table creation.");

        command.CommandText = """
            CREATE TABLE DEV_TEMPLATE.HR_LEAVE_MOVEMENT (
              ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY,
              BALANCE_ID NUMBER(19) NOT NULL, EMPLOYEE_CODE NVARCHAR2(100) NOT NULL,
              LEAVE_TYPE_CODE NVARCHAR2(100) NOT NULL, CYCLE_START_DATE TIMESTAMP(7) NOT NULL,
              SEQUENCE_NO NUMBER(19) NOT NULL, KIND NVARCHAR2(40) NOT NULL,
              OPERATION_KEY NVARCHAR2(64) NOT NULL, PAYLOAD_HASH NVARCHAR2(64) NULL,
              SOURCE_REF NVARCHAR2(300) NOT NULL, REASON NVARCHAR2(1000) NOT NULL,
              ACTOR NVARCHAR2(200) NOT NULL, CREATED_DATE TIMESTAMP(7) NOT NULL,
              ACCRUED_DELTA NUMBER(12,4) NOT NULL, USED_DELTA NUMBER(12,4) NOT NULL,
              CARRIED_DELTA NUMBER(12,4) NOT NULL, ACCRUED_AFTER NUMBER(12,4) NOT NULL,
              USED_AFTER NUMBER(12,4) NOT NULL, CARRIED_AFTER NUMBER(12,4) NOT NULL,
              EXPIRED_DAYS NUMBER(12,4) NOT NULL, TRANSFERRED_DAYS NUMBER(12,4) NOT NULL,
              REVERSES_MOVEMENT_ID NUMBER(19) NULL,
              CONSTRAINT FK_HR_LEAVE_MOVEMENT_BAL FOREIGN KEY (BALANCE_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_BALANCE(ID),
              CONSTRAINT FK_HR_LEAVE_MOVEMENT_REV FOREIGN KEY (REVERSES_MOVEMENT_ID) REFERENCES DEV_TEMPLATE.HR_LEAVE_MOVEMENT(ID),
              CONSTRAINT UX_HR_LEAVE_MOVEMENT_OP UNIQUE (OPERATION_KEY),
              CONSTRAINT UX_HR_LEAVE_MOVEMENT_SEQ UNIQUE (BALANCE_ID,SEQUENCE_NO),
              CONSTRAINT UX_HR_LEAVE_MOVEMENT_REV UNIQUE (REVERSES_MOVEMENT_ID))
            """;
        await command.ExecuteNonQueryAsync();
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        await ValidateLedgerKeysAsync(connection);
        await ProtectHistory(connection, report);
        report.Add("Created DEV_TEMPLATE.HR_LEAVE_MOVEMENT with operation, sequence and reversal uniqueness. No balance data or historical movements changed.");
    }

    private static async Task ValidateLedgerKeysAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM ALL_TAB_IDENTITY_COLS
            WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_MOVEMENT' AND COLUMN_NAME='ID'
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("HR_LEAVE_MOVEMENT.ID must be an identity column.");

        command.CommandText = """
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_LEAVE_MOVEMENT'
              AND c.CONSTRAINT_TYPE='P' AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
              AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
              AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='ID' AND cc.POSITION=1)
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("HR_LEAVE_MOVEMENT must have one enabled, validated single-column primary key on ID.");

        command.CommandText = """
            SELECT COUNT(*) FROM ALL_CONSTRAINTS
            WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_MOVEMENT' AND STATUS='ENABLED' AND VALIDATED='VALIDATED'
              AND ((CONSTRAINT_NAME='FK_HR_LEAVE_MOVEMENT_BAL' AND CONSTRAINT_TYPE='R')
                OR (CONSTRAINT_NAME='FK_HR_LEAVE_MOVEMENT_REV' AND CONSTRAINT_TYPE='R')
                OR (CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_OP' AND CONSTRAINT_TYPE='U')
                OR (CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_SEQ' AND CONSTRAINT_TYPE='U')
                OR (CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_REV' AND CONSTRAINT_TYPE='U'))
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 5)
            throw new InvalidOperationException("HR_LEAVE_MOVEMENT requires two enabled validated FKs and all three unique constraints.");

        command.CommandText = """
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_LEAVE_MOVEMENT'
              AND ((c.CONSTRAINT_NAME='FK_HR_LEAVE_MOVEMENT_BAL' AND c.CONSTRAINT_TYPE='R' AND c.R_OWNER='DEV_TEMPLATE' AND c.DELETE_RULE='NO ACTION'
                    AND EXISTS (SELECT 1 FROM ALL_CONSTRAINTS p JOIN ALL_CONS_COLUMNS pc ON pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME
                                WHERE p.OWNER=c.R_OWNER AND p.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME AND p.TABLE_NAME='HR_LEAVE_BALANCE' AND p.CONSTRAINT_TYPE IN ('P','U')
                                  AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED' AND pc.COLUMN_NAME='ID' AND pc.POSITION=1
                                  AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS px WHERE px.OWNER=p.OWNER AND px.TABLE_NAME=p.TABLE_NAME AND px.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1)
                    AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='BALANCE_ID' AND cc.POSITION=1)
                    AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1)
                OR (c.CONSTRAINT_NAME='FK_HR_LEAVE_MOVEMENT_REV' AND c.CONSTRAINT_TYPE='R' AND c.R_OWNER='DEV_TEMPLATE' AND c.DELETE_RULE='NO ACTION'
                    AND EXISTS (SELECT 1 FROM ALL_CONSTRAINTS p JOIN ALL_CONS_COLUMNS pc ON pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME
                                WHERE p.OWNER=c.R_OWNER AND p.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME AND p.TABLE_NAME='HR_LEAVE_MOVEMENT' AND p.CONSTRAINT_TYPE='P'
                                  AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED' AND pc.COLUMN_NAME='ID' AND pc.POSITION=1
                                  AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS px WHERE px.OWNER=p.OWNER AND px.TABLE_NAME=p.TABLE_NAME AND px.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1)
                    AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='REVERSES_MOVEMENT_ID' AND cc.POSITION=1)
                    AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1))
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 2)
            throw new InvalidOperationException("HR_LEAVE_MOVEMENT FKs must reference the expected enabled parent keys with NO ACTION.");

        command.CommandText = """
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_LEAVE_MOVEMENT' AND c.CONSTRAINT_TYPE='U'
              AND ((c.CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_OP' AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='OPERATION_KEY')
                OR (c.CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_SEQ' AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='BALANCE_ID,SEQUENCE_NO')
                OR (c.CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_REV' AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='REVERSES_MOVEMENT_ID'))
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 3)
            throw new InvalidOperationException("HR_LEAVE_MOVEMENT unique constraints do not match the expected operation, sequence and reversal signatures.");
    }

    private static async Task ProtectHistory(OracleConnection connection, List<string> report)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE OR REPLACE TRIGGER DEV_TEMPLATE.TR_HR_LEAVE_MOVEMENT_IMM
            BEFORE UPDATE OR DELETE ON DEV_TEMPLATE.HR_LEAVE_MOVEMENT FOR EACH ROW
            BEGIN RAISE_APPLICATION_ERROR(-20071, 'Leave ledger is append-only; use a linked reversal.'); END;
            """;
        await command.ExecuteNonQueryAsync();
        report.Add("DEV_TEMPLATE append-only trigger compiled for HR_LEAVE_MOVEMENT; this alone does not prove DML protection while foreign triggers are attached.");
        await ReportForeignInvalidTriggers(connection, report);
    }

    private static async Task ValidateHistoryTriggerTarget(OracleConnection connection, List<string> report)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TABLE_OWNER,TABLE_NAME,TRIGGER_TYPE,TRIGGERING_EVENT
            FROM ALL_TRIGGERS
            WHERE OWNER='DEV_TEMPLATE' AND TRIGGER_NAME='TR_HR_LEAVE_MOVEMENT_IMM'
            """;
        var exists = false;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                exists = true;
                var tableOwner = reader.GetString(0);
                var tableName = reader.GetString(1);
                var triggerType = reader.GetString(2);
                var triggeringEvent = reader.GetString(3);
                if (await reader.ReadAsync())
                    throw new InvalidOperationException("Multiple DEV_TEMPLATE.TR_HR_LEAVE_MOVEMENT_IMM trigger metadata rows were found.");
                if (tableOwner != "DEV_TEMPLATE" || tableName != "HR_LEAVE_MOVEMENT" ||
                    triggerType != "BEFORE EACH ROW" ||
                    !triggeringEvent.Contains("UPDATE", StringComparison.Ordinal) ||
                    !triggeringEvent.Contains("DELETE", StringComparison.Ordinal))
                    throw new InvalidOperationException("DEV_TEMPLATE.TR_HR_LEAVE_MOVEMENT_IMM exists with a different target or event definition; append-only trigger replacement was refused.");
            }
        }
        if (exists)
        {
            await HrTriggerDefinitionGuard.RequireBodyAsync(connection, "TR_HR_LEAVE_MOVEMENT_IMM", "BEGIN RAISE_APPLICATION_ERROR(-20071, 'Leave ledger is append-only; use a linked reversal.'); END;");
            report.Add("Verified append-only trigger name is attached to HR_LEAVE_MOVEMENT for row UPDATE/DELETE and has the expected body before replacement.");
        }
    }

    private static async Task ReportForeignInvalidTriggers(OracleConnection connection, List<string> report)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.OWNER,t.TRIGGER_NAME FROM ALL_TRIGGERS t
            JOIN ALL_OBJECTS o ON o.OWNER=t.OWNER AND o.OBJECT_NAME=t.TRIGGER_NAME AND o.OBJECT_TYPE='TRIGGER'
            WHERE t.TABLE_OWNER='DEV_TEMPLATE' AND t.TABLE_NAME='HR_LEAVE_MOVEMENT'
              AND t.OWNER<>'DEV_TEMPLATE' AND t.STATUS='ENABLED'
              AND (o.STATUS<>'VALID' OR EXISTS (SELECT 1 FROM ALL_ERRORS e
                  WHERE e.OWNER=t.OWNER AND e.NAME=t.TRIGGER_NAME AND e.TYPE='TRIGGER'))
            ORDER BY t.OWNER,t.TRIGGER_NAME
            """;
        var foreignTriggers = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) foreignTriggers.Add($"{reader.GetString(0)}.{reader.GetString(1)}");

        if (foreignTriggers.Count > 0)
            report.Add("WARNING: enabled invalid foreign triggers are attached to DEV_TEMPLATE.HR_LEAVE_MOVEMENT; UPDATE/DELETE protection cannot be accepted until reviewed: " + string.Join(", ", foreignTriggers) + ".");
        else
            report.Add("No enabled invalid foreign triggers found on DEV_TEMPLATE.HR_LEAVE_MOVEMENT; run the rollback-only verifier to confirm the ORA-20071 guard.");
    }
}
