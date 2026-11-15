using Oracle.ManagedDataAccess.Client;

internal static class HrPayrollCorrectionIndexSchema
{
    private const string IndexName = "UX_HR_PAYRUN_CORR_VER";
    private const string TableName = "HR_PAYROLL_RUN";
    private const string CreateIndexSql = "CREATE UNIQUE INDEX DEV_TEMPLATE.UX_HR_PAYRUN_CORR_VER ON DEV_TEMPLATE.HR_PAYROLL_RUN (CASE WHEN CORRECTION_ROOT_RUN_ID IS NOT NULL THEN CORRECTION_ROOT_RUN_ID END, CASE WHEN CORRECTION_ROOT_RUN_ID IS NOT NULL THEN CORRECTION_VERSION END)";
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new(TableName, "CORRECTION_ROOT_RUN_ID", "NUMBER", "Y", Precision: 19, Scale: 0),
        new(TableName, "CORRECTION_VERSION", "NUMBER", "N", Precision: 10, Scale: 0, DefaultExpression: "1")
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await using (var session = connection.CreateCommand())
        {
            session.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
            await session.ExecuteNonQueryAsync();
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);

        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        async Task Execute(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        async Task<List<string>> ReadIndexExpressions()
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COLUMN_EXPRESSION FROM ALL_IND_EXPRESSIONS WHERE INDEX_OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_PAYRUN_CORR_VER' ORDER BY COLUMN_POSITION";
            command.InitialLONGFetchSize = -1;
            await using var reader = await command.ExecuteReaderAsync();
            var expressions = new List<string>();
            while (await reader.ReadAsync())
                expressions.Add(reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0)) ?? string.Empty);
            return expressions;
        }

        report.Add("Checking payroll correction root/version columns on DEV_TEMPLATE.");
        if (await Count("SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_PAYROLL_RUN' AND COLUMN_NAME IN ('CORRECTION_ROOT_RUN_ID','CORRECTION_VERSION')") != 2)
            throw new InvalidOperationException("Payroll correction root/version columns must be installed before their uniqueness index.");

        report.Add("Checking for duplicate non-root correction family/version pairs.");
        var duplicateFamilies = await Count("SELECT COUNT(*) FROM (SELECT CORRECTION_ROOT_RUN_ID,CORRECTION_VERSION FROM DEV_TEMPLATE.HR_PAYROLL_RUN WHERE CORRECTION_ROOT_RUN_ID IS NOT NULL GROUP BY CORRECTION_ROOT_RUN_ID,CORRECTION_VERSION HAVING COUNT(*)>1)");
        if (duplicateFamilies != 0)
            throw new InvalidOperationException("Cannot install payroll correction uniqueness: duplicate non-root lineage/version pairs already exist.");

        report.Add("Inspecting the existing payroll correction uniqueness index.");
        var currentExpressions = await ReadIndexExpressions();
        static string NormalizeExpression(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '"').Select(char.ToUpperInvariant).ToArray());
        var expectedExpressions = new[]
        {
            "CASE WHEN CORRECTION_ROOT_RUN_ID IS NOT NULL THEN CORRECTION_ROOT_RUN_ID END",
            "CASE WHEN CORRECTION_ROOT_RUN_ID IS NOT NULL THEN CORRECTION_VERSION END"
        };
        var correctIndex = await Count("SELECT COUNT(*) FROM ALL_INDEXES i WHERE i.OWNER='DEV_TEMPLATE' AND i.INDEX_NAME='UX_HR_PAYRUN_CORR_VER' AND i.TABLE_NAME='HR_PAYROLL_RUN' AND i.UNIQUENESS='UNIQUE' AND i.STATUS='VALID'") == 1
            && currentExpressions.Count == expectedExpressions.Length
            && currentExpressions.Where((value, index) => NormalizeExpression(value) != NormalizeExpression(expectedExpressions[index])).Any() == false
            && await Count("SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_PAYRUN_CORR_VER' AND INDEX_TYPE='FUNCTION-BASED NORMAL'") == 1;
        if (correctIndex)
        {
            report.Add("Payroll correction uniqueness index already excludes root rows and enforces correction family/version uniqueness.");
            return;
        }

        var oldConstraint = false;
        await using (var namedConstraint = connection.CreateCommand())
        {
            namedConstraint.CommandText = "SELECT TABLE_NAME,CONSTRAINT_TYPE,STATUS,VALIDATED,(SELECT LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME) FROM ALL_CONSTRAINTS c WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='UX_HR_PAYRUN_CORR_VER'";
            await using var reader = await namedConstraint.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                oldConstraint = true;
                if (reader.GetString(0) != TableName || reader.GetString(1) != "U" ||
                    reader.GetString(2) != "ENABLED" || reader.GetString(3) != "VALIDATED" ||
                    reader.IsDBNull(4) || reader.GetString(4) != "CORRECTION_ROOT_RUN_ID,CORRECTION_VERSION" || await reader.ReadAsync())
                    throw new InvalidOperationException("UX_HR_PAYRUN_CORR_VER is reserved by an incompatible constraint; no correction index DDL applied.");
            }
        }
        if (oldConstraint && await Count("SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_TYPE='R' AND R_OWNER='DEV_TEMPLATE' AND R_CONSTRAINT_NAME='UX_HR_PAYRUN_CORR_VER'") != 0)
            throw new InvalidOperationException("UX_HR_PAYRUN_CORR_VER is referenced by a foreign key; it cannot be replaced by a function-based index.");

        var oldIndex = false;
        await using (var namedIndex = connection.CreateCommand())
        {
            namedIndex.CommandText = "SELECT TABLE_NAME,UNIQUENESS,STATUS,INDEX_TYPE,(SELECT LISTAGG(ic.COLUMN_NAME,',') WITHIN GROUP (ORDER BY ic.COLUMN_POSITION) FROM ALL_IND_COLUMNS ic WHERE ic.INDEX_OWNER=i.OWNER AND ic.INDEX_NAME=i.INDEX_NAME) FROM ALL_INDEXES i WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_PAYRUN_CORR_VER'";
            await using var reader = await namedIndex.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                oldIndex = true;
                if (reader.GetString(0) != TableName || reader.GetString(1) != "UNIQUE" || reader.GetString(2) != "VALID" ||
                    reader.GetString(3) != "NORMAL" || reader.IsDBNull(4) || reader.GetString(4) != "CORRECTION_ROOT_RUN_ID,CORRECTION_VERSION" || await reader.ReadAsync())
                    throw new InvalidOperationException("UX_HR_PAYRUN_CORR_VER is used by an incompatible index; no correction index DDL applied.");
            }
        }

        if (oldConstraint)
        {
            report.Add("Replacing the legacy correction-version unique constraint.");
            await Execute("ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN DROP CONSTRAINT UX_HR_PAYRUN_CORR_VER");
        }
        else if (oldIndex)
        {
            report.Add("Replacing the legacy correction-version index.");
            await Execute("DROP INDEX DEV_TEMPLATE.UX_HR_PAYRUN_CORR_VER");
        }

        try
        {
            report.Add("Creating the root-aware function-based unique index.");
            await Execute(CreateIndexSql);
        }
        catch (Exception createError)
        {
            // Oracle DDL auto-commits; restore either legacy form if creating the replacement fails.
            try
            {
                if (oldConstraint)
                    await Execute("ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN ADD CONSTRAINT UX_HR_PAYRUN_CORR_VER UNIQUE (CORRECTION_ROOT_RUN_ID,CORRECTION_VERSION)");
                else if (oldIndex)
                    await Execute("CREATE UNIQUE INDEX DEV_TEMPLATE.UX_HR_PAYRUN_CORR_VER ON DEV_TEMPLATE.HR_PAYROLL_RUN (CORRECTION_ROOT_RUN_ID,CORRECTION_VERSION)");
            }
            catch (Exception restoreError)
            {
                throw new AggregateException("The correction index replacement failed and restoring its legacy uniqueness object also failed.", createError, restoreError);
            }
            throw;
        }

        var installedExpressions = await ReadIndexExpressions();
        var installedIndexCount = await Count("SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_PAYRUN_CORR_VER' AND TABLE_NAME='HR_PAYROLL_RUN' AND UNIQUENESS='UNIQUE' AND STATUS='VALID'");
        report.Add($"Correction index metadata: valid-indexes={installedIndexCount}; function-expressions={installedExpressions.Count}");
        if (installedIndexCount != 1
            || installedExpressions.Count != expectedExpressions.Length
            || installedExpressions.Where((value, index) => NormalizeExpression(value) != NormalizeExpression(expectedExpressions[index])).Any()
            || await Count("SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_PAYRUN_CORR_VER' AND INDEX_TYPE='FUNCTION-BASED NORMAL'") != 1)
            throw new InvalidOperationException("Payroll correction function-based unique index was not created in the expected valid shape.");

        report.Add("Replaced the root/version unique constraint with a function-based unique index: root rows can each use version 1, while non-root correction family/version pairs remain unique.");
    }
}
