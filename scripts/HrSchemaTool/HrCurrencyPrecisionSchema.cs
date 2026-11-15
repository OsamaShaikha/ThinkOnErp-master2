using Oracle.ManagedDataAccess.Client;

internal static class HrCurrencyPrecisionSchema
{
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_PAYROLL_CURRENCY_PRECISION", "CURRENCY_CODE", "VARCHAR2", "N", CharacterLength: 10, DataLength: 10, CharacterSemantics: "B", ExactCharacterLength: true),
        new("HR_PAYROLL_CURRENCY_PRECISION", "DECIMAL_PLACES", "NUMBER", "N", Precision: 2, Scale: 0),
        new("HR_PAYROLL_CURRENCY_PRECISION", "CREATION_USER", "VARCHAR2", "N", CharacterLength: 100, DataLength: 100, CharacterSemantics: "B", ExactCharacterLength: true),
        new("HR_PAYROLL_CURRENCY_PRECISION", "CREATION_DATE", "TIMESTAMP(7)", "N"),
        new("HR_PAYROLL_CURRENCY_PRECISION", "UPDATE_USER", "VARCHAR2", "Y", CharacterLength: 100, DataLength: 100, CharacterSemantics: "B", ExactCharacterLength: true),
        new("HR_PAYROLL_CURRENCY_PRECISION", "UPDATE_DATE", "TIMESTAMP(7)", "Y")
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        const string tableName = "HR_PAYROLL_CURRENCY_PRECISION";
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: true,
            allowMissingTables: new HashSet<string>([tableName], StringComparer.Ordinal));
        var table = await ScalarLong(connection,
            $"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{tableName}'");
        if (table == 0)
        {
            await EnsureConstraintNamesAvailable(connection);
            await Execute(connection, """
                CREATE TABLE DEV_TEMPLATE.HR_PAYROLL_CURRENCY_PRECISION
                (
                    CURRENCY_CODE VARCHAR2(10 BYTE) NOT NULL,
                    DECIMAL_PLACES NUMBER(2) NOT NULL,
                    CREATION_USER VARCHAR2(100 BYTE) NOT NULL,
                    CREATION_DATE TIMESTAMP(7) NOT NULL,
                    UPDATE_USER VARCHAR2(100 BYTE) NULL,
                    UPDATE_DATE TIMESTAMP(7) NULL,
                    CONSTRAINT PK_HR_PAY_CURRENCY_PRECISION PRIMARY KEY (CURRENCY_CODE),
                    CONSTRAINT CK_HR_PAY_CURRENCY_DECIMALS CHECK (DECIMAL_PLACES BETWEEN 0 AND 4)
                )
                """);
            report.Add($"CREATED DEV_TEMPLATE.{tableName}; precision overrides are schema-scoped and empty by default");
        }

        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);

        var columns = await ScalarLong(connection,
            $"SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{tableName}' AND ((COLUMN_NAME='CURRENCY_CODE' AND DATA_TYPE='VARCHAR2' AND DATA_LENGTH=10) OR (COLUMN_NAME='DECIMAL_PLACES' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=2 AND DATA_SCALE=0) OR (COLUMN_NAME='CREATION_USER' AND DATA_TYPE='VARCHAR2' AND DATA_LENGTH=100) OR COLUMN_NAME IN ('CREATION_DATE','UPDATE_USER','UPDATE_DATE'))");
        if (columns != 6)
            throw new InvalidOperationException($"DEV_TEMPLATE.{tableName} does not match the expected six-column schema-scoped model; review before continuing.");

        await VerifyConstraints(connection);

        report.Add($"VERIFIED DEV_TEMPLATE.{tableName}: six mapped columns, schema-scoped currency primary key, decimals 0..4");
    }

    private static async Task<long> ScalarLong(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 15;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task EnsureConstraintNamesAvailable(OracleConnection connection)
    {
        foreach (var name in new[] { "PK_HR_PAY_CURRENCY_PRECISION", "CK_HR_PAY_CURRENCY_DECIMALS" })
        {
            var exists = await ScalarLong(connection,
                "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='" + name + "'");
            if (exists != 0)
                throw new InvalidOperationException($"Constraint name {name} is already used in DEV_TEMPLATE; currency precision DDL was not applied.");
        }

        var primaryKeyIndex = await ScalarLong(connection,
            "SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='PK_HR_PAY_CURRENCY_PRECISION'");
        if (primaryKeyIndex != 0)
            throw new InvalidOperationException("Index name PK_HR_PAY_CURRENCY_PRECISION is already used in DEV_TEMPLATE; currency precision DDL was not applied.");
    }

    private static async Task VerifyConstraints(OracleConnection connection)
    {
        var primaryKey = await ScalarLong(connection, """
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_PAYROLL_CURRENCY_PRECISION'
              AND c.CONSTRAINT_NAME='PK_HR_PAY_CURRENCY_PRECISION' AND c.CONSTRAINT_TYPE='P'
              AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
              AND (SELECT LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION)
                    FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='CURRENCY_CODE'
            """);
        var check = await ScalarLong(connection, """
            SELECT COUNT(*) FROM ALL_CONSTRAINTS c
            WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_PAYROLL_CURRENCY_PRECISION'
              AND c.CONSTRAINT_NAME='CK_HR_PAY_CURRENCY_DECIMALS' AND c.CONSTRAINT_TYPE='C'
              AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
              AND REPLACE(REPLACE(REPLACE(UPPER(c.SEARCH_CONDITION_VC),' ',''),'(',''),')','')='DECIMAL_PLACESBETWEEN0AND4'
            """);
        if (primaryKey != 1 || check != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_PAYROLL_CURRENCY_PRECISION requires the enabled, validated company/currency primary key and exact 0..4 decimal check.");
    }

    private static async Task Execute(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        await command.ExecuteNonQueryAsync();
    }
}
