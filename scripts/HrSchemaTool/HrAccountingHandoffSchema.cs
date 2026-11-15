using Oracle.ManagedDataAccess.Client;

internal static class HrAccountingHandoffSchema
{
    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await using var session = connection.CreateCommand();
        session.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = DEV_TEMPLATE";
        await session.ExecuteNonQueryAsync();
        async Task<long> Count(string sql)
        {
            await using var c = connection.CreateCommand(); c.CommandText = sql;
            return Convert.ToInt64(await c.ExecuteScalarAsync());
        }
        async Task Run(string table, string ddl)
        {
            if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") != 0)
            { report.Add($"Preserved existing {table}; no structure rewritten."); return; }
            await using var c = connection.CreateCommand(); c.CommandText = ddl; await c.ExecuteNonQueryAsync();
            report.Add($"Created {table}.");
        }
        async Task AddColumn(string table, string column, string ddl, string expectedDefinition)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(table, "^[A-Z0-9_]+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(column, "^[A-Z0-9_]+$"))
                throw new InvalidOperationException("Invalid identifier in handoff column definition.");

            var exists = await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='{column}'");
            if (exists == 0)
            {
                await using var c = connection.CreateCommand(); c.CommandText = ddl; await c.ExecuteNonQueryAsync();
                report.Add($"Added {table}.{column}.");
            }
            var compatible = await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='{column}' AND {expectedDefinition}");
            if (compatible != 1)
                throw new InvalidOperationException($"Existing or newly added {table}.{column} has an incompatible definition.");
            report.Add($"Verified {table}.{column}: {expectedDefinition}.");
        }
        async Task PreflightAddColumns()
        {
            // These additions run after the handoff tables are created. Check every
            // pre-existing target before the first CREATE/ALTER so a late mismatch
            // cannot leave a partially installed handoff schema.
            var additions = new (string Table, string Column, string Definition)[]
            {
                ("HR_EMPLOYEE_LOAN", "CURRENCY_CODE", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=20 AND NULLABLE='Y' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'"),
                ("HR_EMPLOYEE_ADVANCE", "CURRENCY_CODE", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=20 AND NULLABLE='Y' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'"),
                ("HR_EMPLOYEE_ADVANCE", "RESERVED_BY_HANDOFF_ID", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'"),
                ("HR_LOAN_REPAYMENT_SCHEDULE", "RESERVED_BY_HANDOFF_ID", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'"),
                ("HR_ACCOUNTING_HANDOFF", "PAY_PERIOD", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=40 AND NULLABLE='Y'"),
                ("HR_ACCOUNTING_HANDOFF", "SOURCE_APPROVED_AT_UTC", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "PAYMENT_METHOD_CODE", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=50 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "FAILURE_REASON", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "FAILED_AT_UTC", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "FAILED_BY", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "REVERSAL_REASON", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "REVERSAL_EXTERNAL_REFERENCE", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "REVERSAL_EVIDENCE_REFERENCE", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=500 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "REVERSED_AT_UTC", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "REVERSED_BY", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_PAYMENT", "REVERSAL_VOUCHER_ID", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'"),
                ("ACC_HANDOFF_POSTING_RECEIPT", "SETTLEMENT_SNAPSHOT_JSON", "DATA_TYPE='CLOB' AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "FUNDING_VOUCHER_ID", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "SOURCE_VERSION", "DATA_TYPE='NUMBER' AND DATA_PRECISION=10 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='N'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_VOUCHER_ID", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_EVENT_KEY", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=160 AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_REASON", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_EXTERNAL_REFERENCE", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_EVIDENCE_REFERENCE", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=500 AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "REVERSED_AT_UTC", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'"),
                ("ACC_LOAN_FUNDING_RECEIPT", "REVERSED_BY", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'"),
                ("HR_PAYROLL_RUN", "CORRECTION_OF_RUN_ID", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'"),
                ("HR_PAYROLL_RUN", "CORRECTION_ROOT_RUN_ID", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'"),
                ("HR_PAYROLL_RUN", "CORRECTION_VERSION", "DATA_TYPE='NUMBER' AND DATA_PRECISION=10 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='N'"),
                ("HR_PAYROLL_RUN", "CORRECTION_REASON", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'")
            };
            var createdByInstaller = new HashSet<string>(StringComparer.Ordinal)
            {
                "HR_ACCOUNTING_HANDOFF", "ACC_HANDOFF_PAYMENT", "ACC_HANDOFF_POSTING_RECEIPT", "ACC_LOAN_FUNDING_RECEIPT"
            };
            var requiredTables = new HashSet<string>(StringComparer.Ordinal)
            {
                "HR_EMPLOYEE_LOAN", "HR_EMPLOYEE_ADVANCE", "HR_LOAN_REPAYMENT_SCHEDULE", "HR_PAYROLL_RUN", "GL_VOUCHER_HEADER"
            };
            foreach (var table in requiredTables)
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") != 1)
                    throw new InvalidOperationException($"Required table DEV_TEMPLATE.{table} is missing; handoff install stopped before DDL.");

            foreach (var (table, column, definition) in additions)
            {
                var tableExists = await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'");
                if (tableExists == 0)
                {
                    if (createdByInstaller.Contains(table)) continue;
                    throw new InvalidOperationException($"Required table DEV_TEMPLATE.{table} is missing; handoff install stopped before DDL.");
                }
                var columnExists = await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='{column}'");
                if (columnExists == 0) continue;
                var compatible = await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='{column}' AND {definition}");
                if (compatible != 1)
                    throw new InvalidOperationException($"Existing {table}.{column} has an incompatible definition; handoff install stopped before DDL.");
            }
            report.Add($"Preflighted {additions.Length} handoff add-column definitions before DDL.");
        }
        async Task<(string Type, string Status, string Validated, string? DeleteRule, string Columns, string? ReferencedOwner, string? ReferencedTable, string? ReferencedColumns)?> ReadConstraint(string table, string constraint)
        {
            await using var c = connection.CreateCommand();
            c.CommandText = """
                SELECT C.CONSTRAINT_TYPE,C.STATUS,C.VALIDATED,C.DELETE_RULE,
                       LISTAGG(CC.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY CC.POSITION),
                       R.OWNER,R.TABLE_NAME,
                       LISTAGG(RCC.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY CC.POSITION)
                FROM ALL_CONSTRAINTS C
                JOIN ALL_CONS_COLUMNS CC ON CC.OWNER=C.OWNER AND CC.CONSTRAINT_NAME=C.CONSTRAINT_NAME
                LEFT JOIN ALL_CONSTRAINTS R ON R.OWNER=C.R_OWNER AND R.CONSTRAINT_NAME=C.R_CONSTRAINT_NAME
                LEFT JOIN ALL_CONS_COLUMNS RCC ON RCC.OWNER=R.OWNER AND RCC.CONSTRAINT_NAME=R.CONSTRAINT_NAME AND RCC.POSITION=CC.POSITION
                WHERE C.OWNER='DEV_TEMPLATE' AND C.TABLE_NAME=:tableName AND C.CONSTRAINT_NAME=:constraintName
                GROUP BY C.CONSTRAINT_TYPE,C.STATUS,C.VALIDATED,C.DELETE_RULE,R.OWNER,R.TABLE_NAME
                """;
            c.Parameters.Add(new OracleParameter("tableName", table));
            c.Parameters.Add(new OracleParameter("constraintName", constraint));
            await using var reader = await c.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return (reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7));
        }
        async Task EnsureConstraint(string table, string constraint, string ddl, string expectedType,
            string expectedColumns, string? expectedReferencedTable = null, string? expectedReferencedColumns = null)
        {
            var actual = await ReadConstraint(table, constraint);
            if (actual == null)
            {
                await using var c = connection.CreateCommand(); c.CommandText = ddl; await c.ExecuteNonQueryAsync();
                actual = await ReadConstraint(table, constraint);
                report.Add($"Added constraint {constraint}.");
            }
            if (actual == null || actual.Value.Type != expectedType || actual.Value.Status != "ENABLED" ||
                actual.Value.Validated != "VALIDATED" || actual.Value.Columns != expectedColumns ||
                actual.Value.ReferencedOwner != (expectedReferencedTable is null ? null : "DEV_TEMPLATE") ||
                actual.Value.ReferencedTable != expectedReferencedTable || actual.Value.ReferencedColumns != expectedReferencedColumns ||
                (expectedType == "R" && actual.Value.DeleteRule != "NO ACTION"))
                throw new InvalidOperationException($"Existing or newly added constraint {constraint} has an incompatible definition.");
            report.Add($"Verified constraint {constraint}: {expectedColumns}.");
        }
        async Task EnsureUniqueColumns(string table, string columns, string constraint, string ddl)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(table, "^[A-Z0-9_]+$") ||
                !columns.Split(',').All(column => System.Text.RegularExpressions.Regex.IsMatch(column, "^[A-Z0-9_]+$")))
                throw new InvalidOperationException("Invalid identifier in handoff unique constraint definition.");

            // Prefer exact named-definition validation whenever this install owns the name.
            var named = await ReadConstraint(table, constraint);
            if (named != null)
            {
                await EnsureConstraint(table, constraint, ddl, "U", columns);
                return;
            }

            var equivalent = await Count($"""
                SELECT COUNT(*) FROM ALL_CONSTRAINTS c
                WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='{table}'
                  AND c.CONSTRAINT_TYPE='U' AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                  AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION)
                       FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='{columns}'
                """);
            if (equivalent == 0)
            {
                await EnsureConstraint(table, constraint, ddl, "U", columns);
                return;
            }
            report.Add($"Verified unique constraint columns {table}({columns}); existing constraint name preserved.");
        }
        async Task EnsurePrimaryKey(string table, string constraintName)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(table, "^[A-Z0-9_]+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(constraintName, "^[A-Z0-9_]{1,30}$"))
                throw new InvalidOperationException("Invalid identifier in handoff primary key definition.");
            var primaryKeys = await Count($"""
                SELECT COUNT(*) FROM ALL_CONSTRAINTS c
                WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='{table}'
                  AND c.CONSTRAINT_TYPE='P' AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                  AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION)
                       FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='ID'
                """);
            if (primaryKeys == 0)
            {
                await using var c = connection.CreateCommand();
                c.CommandText = $"ALTER TABLE DEV_TEMPLATE.{table} ADD CONSTRAINT {constraintName} PRIMARY KEY (ID)";
                await c.ExecuteNonQueryAsync();
                report.Add($"Added ID primary key for {table}.");
                primaryKeys = await Count($"""
                    SELECT COUNT(*) FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='{table}'
                      AND c.CONSTRAINT_TYPE='P' AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                      AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION)
                           FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='ID'
                    """);
            }
            if (primaryKeys != 1)
                throw new InvalidOperationException($"{table} must have one enabled, validated primary key on ID.");
        }
        async Task EnsureIndex(string table, string indexName, string columns, string ddl)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(table, "^[A-Z0-9_]+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(indexName, "^[A-Z0-9_]+$") ||
                !columns.Split(',').All(column => System.Text.RegularExpressions.Regex.IsMatch(column, "^[A-Z0-9_]+$")))
                throw new InvalidOperationException("Invalid identifier in handoff index definition.");

            var exact = await Count($"""
                SELECT COUNT(*) FROM ALL_INDEXES i
                WHERE i.OWNER='DEV_TEMPLATE' AND i.INDEX_NAME='{indexName}' AND i.TABLE_NAME='{table}'
                  AND i.UNIQUENESS='NONUNIQUE' AND i.STATUS='VALID'
                  AND (SELECT LISTAGG(ic.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY ic.COLUMN_POSITION)
                       FROM ALL_IND_COLUMNS ic WHERE ic.INDEX_OWNER=i.OWNER AND ic.INDEX_NAME=i.INDEX_NAME)='{columns}'
                """);
            if (exact == 1)
            {
                report.Add($"Verified index {indexName}: {table}({columns}).");
                return;
            }

            var named = await Count($"SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='{indexName}'");
            if (named > 0)
                throw new InvalidOperationException($"Existing handoff index {indexName} has an incompatible definition.");
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ddl;
                await c.ExecuteNonQueryAsync();
            }
            exact = await Count($"""
                SELECT COUNT(*) FROM ALL_INDEXES i
                WHERE i.OWNER='DEV_TEMPLATE' AND i.INDEX_NAME='{indexName}' AND i.TABLE_NAME='{table}'
                  AND i.UNIQUENESS='NONUNIQUE' AND i.STATUS='VALID'
                  AND (SELECT LISTAGG(ic.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY ic.COLUMN_POSITION)
                       FROM ALL_IND_COLUMNS ic WHERE ic.INDEX_OWNER=i.OWNER AND ic.INDEX_NAME=i.INDEX_NAME)='{columns}'
                """);
            if (exact != 1)
                throw new InvalidOperationException($"New handoff index {indexName} failed definition validation.");
            report.Add($"Added and verified index {indexName}: {table}({columns}).");
        }
        async Task DropConstraintIfPresent(string table, string constraint, string expectedType, string expectedColumns)
        {
            var actual = await ReadConstraint(table, constraint);
            if (actual == null) return;
            if (actual.Value.Type != expectedType || actual.Value.Columns != expectedColumns ||
                actual.Value.Status != "ENABLED" || actual.Value.Validated != "VALIDATED" ||
                (expectedType == "R" && actual.Value.DeleteRule != "NO ACTION"))
                throw new InvalidOperationException($"Refusing to replace incompatible constraint {constraint} on {table}.");
            await using var c = connection.CreateCommand(); c.CommandText = $"ALTER TABLE DEV_TEMPLATE.{table} DROP CONSTRAINT {constraint}"; await c.ExecuteNonQueryAsync();
            report.Add($"Replaced constraint {constraint} to support funding versions.");
        }
        async Task PreflightNamedConstraints()
        {
            var plans = new (string Table, string Name, string Type, string Columns, string? RefTable, string? RefColumns)[]
            {
                ("HR_PAYROLL_RUN", "FK_HR_PAYRUN_CORR_OF", "R", "CORRECTION_OF_RUN_ID", "HR_PAYROLL_RUN", "ID"),
                ("HR_PAYROLL_RUN", "FK_HR_PAYRUN_CORR_ROOT", "R", "CORRECTION_ROOT_RUN_ID", "HR_PAYROLL_RUN", "ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_SOURCE", "U", "SOURCE_TYPE,SOURCE_ID", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_SRCVER", "U", "SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_FUNDING_GL_VOUCHER", "R", "FUNDING_VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_VOUCHER", "U", "FUNDING_VOUCHER_ID", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_FUND_REV_GL_VCH", "R", "REVERSAL_VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_EVENT", "U", "REVERSAL_EVENT_KEY", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_REF", "U", "REVERSAL_EXTERNAL_REFERENCE", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_VCH", "U", "REVERSAL_VOUCHER_ID", null, null),
                ("HR_ACCOUNTING_HANDOFF", "UX_HR_HANDOFF_SOURCE", "U", "COMPANY_ID,SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION,OPERATION", null, null),
                ("HR_ACCOUNTING_HANDOFF", "UX_HR_HANDOFF_REF", "U", "COMPANY_ID,HANDOFF_REFERENCE", null, null),
                ("HR_ACCOUNTING_HANDOFF_LINE", "FK_HR_HANDOFF_LINE", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("HR_ACCOUNTING_HANDOFF_TRANSITION", "FK_HR_HANDOFF_TRANS", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("HR_ACCOUNTING_OUTBOX", "FK_HR_HANDOFF_OUTBOX", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_INBOX", "FK_ACC_HANDOFF_INBOX", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_JOURNAL_LINK", "FK_ACC_HANDOFF_JOURNAL", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_JOURNAL_LINK", "FK_ACC_HANDOFF_JV", "R", "VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("ACC_HANDOFF_POSTING_RECEIPT", "FK_ACC_HANDOFF_RECEIPT", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_POSTING_RECEIPT", "FK_ACC_RECEIPT_JV", "R", "VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("ACC_HANDOFF_PAYMENT", "FK_ACC_HANDOFF_PAYMENT", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_KEY", "U", "HANDOFF_ID,IDEMPOTENCY_KEY", null, null),
                ("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_SEQ", "U", "HANDOFF_ID,PAYMENT_SEQUENCE", null, null),
                ("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_REF", "U", "EXTERNAL_REFERENCE", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_LOAN_FUND_RECEIPT", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_HANDOFF", "U", "HANDOFF_ID", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_EVENT", "U", "EVENT_KEY", null, null),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REF", "U", "EXTERNAL_REFERENCE", null, null),
                ("HR_ACCOUNTING_OUTBOX", "UX_HR_OUTBOX_EVENT", "U", "EVENT_KEY", null, null),
                ("ACC_HANDOFF_INBOX", "UX_ACC_INBOX_EVENT", "U", "EVENT_KEY", null, null),
                ("ACC_HANDOFF_JOURNAL_LINK", "UX_ACC_JOURNAL_HANDOFF", "U", "HANDOFF_ID", null, null),
                ("ACC_HANDOFF_JOURNAL_LINK", "UX_ACC_JOURNAL_VOUCHER", "U", "VOUCHER_ID", null, null),
                ("ACC_HANDOFF_POSTING_RECEIPT", "UX_ACC_RECEIPT_HANDOFF", "U", "HANDOFF_ID", null, null),
                ("ACC_HANDOFF_POSTING_RECEIPT", "UX_ACC_RECEIPT_EVENT", "U", "EVENT_KEY", null, null)
            };
            var installerTables = new HashSet<string>(StringComparer.Ordinal)
            {
                "HR_ACCOUNTING_HANDOFF", "HR_ACCOUNTING_HANDOFF_LINE", "HR_ACCOUNTING_HANDOFF_TRANSITION",
                "HR_ACCOUNTING_OUTBOX", "ACC_HANDOFF_INBOX", "ACC_HANDOFF_JOURNAL_LINK",
                "ACC_HANDOFF_POSTING_RECEIPT", "ACC_HANDOFF_PAYMENT", "ACC_LOAN_FUNDING_RECEIPT"
            };
            foreach (var (table, name, type, columns, refTable, refColumns) in plans)
            {
                if (type == "U")
                {
                    var namedIndexes = await Count($"SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='{name}'");
                    if (namedIndexes != 0)
                    {
                        var compatibleIndex = await Count($"""
                            SELECT COUNT(*) FROM ALL_INDEXES i
                            WHERE i.OWNER='DEV_TEMPLATE' AND i.INDEX_NAME='{name}' AND i.TABLE_NAME='{table}'
                              AND i.UNIQUENESS='UNIQUE' AND i.STATUS='VALID'
                              AND (SELECT LISTAGG(ic.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY ic.COLUMN_POSITION)
                                   FROM ALL_IND_COLUMNS ic WHERE ic.INDEX_OWNER=i.OWNER AND ic.INDEX_NAME=i.INDEX_NAME)='{columns}'
                            """);
                        if (compatibleIndex != 1)
                            throw new InvalidOperationException($"Index name {name} is already used by an incompatible index; handoff install stopped before DDL.");
                    }
                }
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") == 0)
                {
                    if (installerTables.Contains(table)) continue;
                    throw new InvalidOperationException($"Required constraint table DEV_TEMPLATE.{table} is missing; handoff install stopped before DDL.");
                }
                var namedElsewhere = await Count($"SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='{name}' AND TABLE_NAME<>'{table}'");
                if (namedElsewhere != 0)
                    throw new InvalidOperationException($"Constraint name {name} is already owned by another table; handoff install stopped before DDL.");
                var actual = await ReadConstraint(table, name);
                if (actual is not null &&
                    (actual.Value.Type != type || actual.Value.Status != "ENABLED" || actual.Value.Validated != "VALIDATED" ||
                     actual.Value.Columns != columns || actual.Value.ReferencedTable != refTable ||
                     actual.Value.ReferencedOwner != (refTable is null ? null : "DEV_TEMPLATE") ||
                     actual.Value.ReferencedColumns != refColumns || (type == "R" && actual.Value.DeleteRule != "NO ACTION")))
                    throw new InvalidOperationException($"Existing constraint {table}.{name} is incompatible; handoff install stopped before DDL.");
            }
            report.Add($"Preflighted {plans.Length} named handoff constraints before DDL.");
        }
        async Task PreflightPrimaryKeysAndIndexes()
        {
            var tables = new[]
            {
                "HR_ACCOUNTING_HANDOFF", "HR_ACCOUNTING_HANDOFF_LINE", "HR_ACCOUNTING_HANDOFF_TRANSITION",
                "HR_ACCOUNTING_OUTBOX", "ACC_HANDOFF_INBOX", "ACC_HANDOFF_JOURNAL_LINK",
                "ACC_HANDOFF_POSTING_RECEIPT", "ACC_HANDOFF_PAYMENT", "ACC_LOAN_FUNDING_RECEIPT"
            };
            foreach (var table in tables)
            {
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") == 0) continue;
                var idColumn = await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'");
                if (idColumn != 1)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table}.ID is missing or incompatible; handoff install stopped before DDL.");
                var primaryKeys = await Count($"SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND CONSTRAINT_TYPE='P'");
                var validIdPrimaryKeys = await Count($"""
                    SELECT COUNT(*) FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='{table}' AND c.CONSTRAINT_TYPE='P'
                      AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                      AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION)
                           FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='ID'
                    """);
                if (primaryKeys > 0 && validIdPrimaryKeys != 1)
                    throw new InvalidOperationException($"Existing primary key on DEV_TEMPLATE.{table} is not a single enabled, validated ID key; handoff install stopped before DDL.");
                if (primaryKeys == 0)
                {
                    var invalidIds = await Count($"SELECT COUNT(*) FROM (SELECT ID FROM DEV_TEMPLATE.{table} GROUP BY ID HAVING ID IS NULL OR COUNT(*)>1)");
                    if (invalidIds != 0)
                        throw new InvalidOperationException($"DEV_TEMPLATE.{table}.ID contains nulls or duplicates; handoff install stopped before DDL.");
                }
            }

            var indexes = new (string Table, string Name, string Columns)[]
            {
                ("HR_ACCOUNTING_HANDOFF_LINE", "IX_HR_HANDOFF_LINE", "HANDOFF_ID"),
                ("HR_ACCOUNTING_HANDOFF_TRANSITION", "IX_HR_HANDOFF_TRANS", "HANDOFF_ID"),
                ("HR_ACCOUNTING_OUTBOX", "IX_HR_HANDOFF_OUTBOX", "STATUS,CREATED_AT_UTC"),
                ("ACC_HANDOFF_INBOX", "IX_ACC_HANDOFF_INBOX", "HANDOFF_ID,RECEIVED_AT_UTC")
            };
            foreach (var (table, name, columns) in indexes)
            {
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") == 0) continue;
                var named = await Count($"SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='{name}'");
                if (named == 0) continue;
                var exact = await Count($"""
                    SELECT COUNT(*) FROM ALL_INDEXES i
                    WHERE i.OWNER='DEV_TEMPLATE' AND i.INDEX_NAME='{name}' AND i.TABLE_NAME='{table}'
                      AND i.UNIQUENESS='NONUNIQUE' AND i.STATUS='VALID'
                      AND (SELECT LISTAGG(ic.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY ic.COLUMN_POSITION)
                           FROM ALL_IND_COLUMNS ic WHERE ic.INDEX_OWNER=i.OWNER AND ic.INDEX_NAME=i.INDEX_NAME)='{columns}'
                    """);
                if (exact != 1)
                    throw new InvalidOperationException($"Existing index {name} has an incompatible definition; handoff install stopped before DDL.");
            }
            report.Add("Preflighted handoff primary keys, ID uniqueness and named indexes before DDL.");
        }
        static IEnumerable<string> SplitDdlColumns(string body)
        {
            var start = 0;
            var depth = 0;
            var inString = false;
            for (var i = 0; i < body.Length; i++)
            {
                var ch = body[i];
                if (ch == '\'')
                {
                    if (inString && i + 1 < body.Length && body[i + 1] == '\'') { i++; continue; }
                    inString = !inString;
                }
                else if (!inString && ch == '(') depth++;
                else if (!inString && ch == ')') depth--;
                else if (!inString && depth == 0 && ch == ',')
                {
                    yield return body[start..i].Trim();
                    start = i + 1;
                }
            }
            if (start < body.Length) yield return body[start..].Trim();
        }
        static HrSchemaColumnExpectation ParseCreateColumn(string table, string definition, bool additive)
        {
            var match = System.Text.RegularExpressions.Regex.Match(definition,
                @"^(?<column>[A-Z][A-Z0-9_$#]*)\s+(?<type>[A-Z][A-Z0-9_]*)(?:\((?<args>\d+(?:\s*,\s*\d+)?)\))?(?<rest>.*)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new InvalidOperationException($"Unsupported handoff CREATE TABLE column definition: {definition}");
            var column = match.Groups["column"].Value.ToUpperInvariant();
            var typeName = match.Groups["type"].Value.ToUpperInvariant();
            var args = match.Groups["args"].Success ? match.Groups["args"].Value.Split(',').Select(x => int.Parse(x.Trim())).ToArray() : [];
            var rest = match.Groups["rest"].Value;
            var notNull = System.Text.RegularExpressions.Regex.IsMatch(rest, @"\bNOT\s+NULL\b|\bPRIMARY\s+KEY\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var nullable = notNull ? "N" : System.Text.RegularExpressions.Regex.IsMatch(rest, @"\bNULL\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? "Y" :
                throw new InvalidOperationException($"Handoff column definition has no explicit nullability: {table}.{column}");
            var defaultMatch = System.Text.RegularExpressions.Regex.Match(rest, @"\bDEFAULT\s+(?<value>[A-Z0-9_+-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var defaultExpression = defaultMatch.Success ? defaultMatch.Groups["value"].Value : null;
            int? characterLength = typeName is "NVARCHAR2" or "VARCHAR2" or "NCHAR" or "CHAR" && args.Length > 0 ? args[0] : null;
            int? precision = typeName == "NUMBER" && args.Length > 0 ? args[0] : null;
            int? scale = typeName == "NUMBER" && args.Length > 1 ? args[1] : typeName == "NUMBER" && args.Length > 0 ? 0 : null;
            var dataType = typeName switch
            {
                "TIMESTAMP" when args.Length == 1 => $"TIMESTAMP({args[0]})",
                _ => typeName
            };
            return new HrSchemaColumnExpectation(table, column, dataType, nullable,
                CharacterLength: characterLength, Precision: precision, Scale: scale,
                Additive: additive, DefaultExpression: defaultExpression,
                ExactCharacterLength: characterLength.HasValue,
                CharacterSemantics: typeName is "NVARCHAR2" or "NCHAR" ? "C" : typeName is "VARCHAR2" or "CHAR" ? "B" : null);
        }
        async Task PreflightHandoffTableColumns(IReadOnlyCollection<(string Table, string Ddl)> tableDdls)
        {
            var installerTables = new HashSet<string>(StringComparer.Ordinal)
            {
                "HR_ACCOUNTING_HANDOFF", "HR_ACCOUNTING_HANDOFF_LINE", "HR_ACCOUNTING_HANDOFF_TRANSITION",
                "HR_ACCOUNTING_OUTBOX", "ACC_HANDOFF_INBOX", "ACC_HANDOFF_JOURNAL_LINK",
                "ACC_HANDOFF_POSTING_RECEIPT", "ACC_HANDOFF_PAYMENT", "ACC_LOAN_FUNDING_RECEIPT"
            };
            var additive = new HashSet<string>(StringComparer.Ordinal)
            {
                "ACC_HANDOFF_PAYMENT.PAYMENT_METHOD_CODE", "ACC_HANDOFF_PAYMENT.FAILURE_REASON",
                "ACC_HANDOFF_PAYMENT.FAILED_AT_UTC", "ACC_HANDOFF_PAYMENT.FAILED_BY",
                "ACC_HANDOFF_PAYMENT.REVERSAL_REASON", "ACC_HANDOFF_PAYMENT.REVERSAL_EXTERNAL_REFERENCE",
                "ACC_HANDOFF_PAYMENT.REVERSAL_EVIDENCE_REFERENCE", "ACC_HANDOFF_PAYMENT.REVERSED_AT_UTC",
                "ACC_HANDOFF_PAYMENT.REVERSED_BY", "ACC_HANDOFF_PAYMENT.REVERSAL_VOUCHER_ID",
                "ACC_HANDOFF_POSTING_RECEIPT.SETTLEMENT_SNAPSHOT_JSON",
                "ACC_LOAN_FUNDING_RECEIPT.FUNDING_VOUCHER_ID", "ACC_LOAN_FUNDING_RECEIPT.SOURCE_VERSION",
                "ACC_LOAN_FUNDING_RECEIPT.REVERSAL_VOUCHER_ID", "ACC_LOAN_FUNDING_RECEIPT.REVERSAL_EVENT_KEY",
                "ACC_LOAN_FUNDING_RECEIPT.REVERSAL_REASON", "ACC_LOAN_FUNDING_RECEIPT.REVERSAL_EXTERNAL_REFERENCE",
                "ACC_LOAN_FUNDING_RECEIPT.REVERSAL_EVIDENCE_REFERENCE", "ACC_LOAN_FUNDING_RECEIPT.REVERSED_AT_UTC",
                "ACC_LOAN_FUNDING_RECEIPT.REVERSED_BY"
            };
            var checkedColumns = 0;
            foreach (var (table, ddl) in tableDdls)
            {
                var open = ddl.IndexOf('(');
                var close = ddl.LastIndexOf(')');
                if (open < 0 || close <= open || !ddl.StartsWith($"CREATE TABLE DEV_TEMPLATE.{table} ", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Unsupported handoff create-table statement for {table}.");
                var expectations = SplitDdlColumns(ddl[(open + 1)..close])
                    .Where(x => !x.StartsWith("CONSTRAINT ", StringComparison.OrdinalIgnoreCase))
                    .Select(x =>
                    {
                        var columnName = System.Text.RegularExpressions.Regex.Match(x, @"^[A-Z][A-Z0-9_$#]*", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Value.ToUpperInvariant();
                        if (columnName.Length == 0) throw new InvalidOperationException($"Could not parse handoff column declaration: {x}");
                        return ParseCreateColumn(table, x, additive.Contains($"{table}.{columnName}"));
                    }).ToArray();
                checkedColumns += expectations.Length;
                await HrSchemaColumnGuard.ValidateAsync(connection, expectations, allowMissingColumns: true,
                    allowMissingTables: installerTables);
            }
            report.Add($"Preflighted {checkedColumns} scalar columns from all handoff CREATE TABLE definitions before DDL.");
        }
        async Task PreflightConstraintData()
        {
            var uniquePlans = new (string Table, string Name, string Columns)[]
            {
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_SOURCE", "SOURCE_TYPE,SOURCE_ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_SRCVER", "SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_VOUCHER", "FUNDING_VOUCHER_ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_EVENT", "REVERSAL_EVENT_KEY"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_REF", "REVERSAL_EXTERNAL_REFERENCE"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_VCH", "REVERSAL_VOUCHER_ID"),
                ("HR_ACCOUNTING_HANDOFF", "UX_HR_HANDOFF_SOURCE", "COMPANY_ID,SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION,OPERATION"),
                ("HR_ACCOUNTING_HANDOFF", "UX_HR_HANDOFF_REF", "COMPANY_ID,HANDOFF_REFERENCE"),
                ("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_KEY", "HANDOFF_ID,IDEMPOTENCY_KEY"),
                ("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_SEQ", "HANDOFF_ID,PAYMENT_SEQUENCE"),
                ("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_REF", "EXTERNAL_REFERENCE"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_HANDOFF", "HANDOFF_ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_EVENT", "EVENT_KEY"),
                ("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REF", "EXTERNAL_REFERENCE"),
                ("HR_ACCOUNTING_OUTBOX", "UX_HR_OUTBOX_EVENT", "EVENT_KEY"),
                ("ACC_HANDOFF_INBOX", "UX_ACC_INBOX_EVENT", "EVENT_KEY"),
                ("ACC_HANDOFF_JOURNAL_LINK", "UX_ACC_JOURNAL_HANDOFF", "HANDOFF_ID"),
                ("ACC_HANDOFF_JOURNAL_LINK", "UX_ACC_JOURNAL_VOUCHER", "VOUCHER_ID"),
                ("ACC_HANDOFF_POSTING_RECEIPT", "UX_ACC_RECEIPT_HANDOFF", "HANDOFF_ID"),
                ("ACC_HANDOFF_POSTING_RECEIPT", "UX_ACC_RECEIPT_EVENT", "EVENT_KEY")
            };
            foreach (var (table, name, columns) in uniquePlans)
            {
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") == 0) continue;
                var existing = await ReadConstraint(table, name);
                if (existing is not null && existing.Value.Type == "U" && existing.Value.Status == "ENABLED" &&
                    existing.Value.Validated == "VALIDATED" && existing.Value.Columns == columns) continue;
                var columnNames = columns.Split(',');
                var columnsExist = await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME IN ('{string.Join("','", columnNames)}')");
                if (columnsExist != columnNames.Length) continue; // The installer adds missing nullable/defaulted columns later.
                var notNull = string.Join(" AND ", columnNames.Select(column => $"{column} IS NOT NULL"));
                var duplicates = await Count($"SELECT COUNT(*) FROM (SELECT {columns} FROM DEV_TEMPLATE.{table} WHERE {notNull} GROUP BY {columns} HAVING COUNT(*)>1)");
                if (duplicates != 0)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table} contains duplicate non-null keys for {columns}; handoff install stopped before DDL.");
            }

            var foreignKeys = new (string Table, string Name, string Column, string RefTable, string RefColumn)[]
            {
                ("HR_PAYROLL_RUN", "FK_HR_PAYRUN_CORR_OF", "CORRECTION_OF_RUN_ID", "HR_PAYROLL_RUN", "ID"),
                ("HR_PAYROLL_RUN", "FK_HR_PAYRUN_CORR_ROOT", "CORRECTION_ROOT_RUN_ID", "HR_PAYROLL_RUN", "ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_FUNDING_GL_VOUCHER", "FUNDING_VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_FUND_REV_GL_VCH", "REVERSAL_VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("HR_ACCOUNTING_HANDOFF_LINE", "FK_HR_HANDOFF_LINE", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("HR_ACCOUNTING_HANDOFF_TRANSITION", "FK_HR_HANDOFF_TRANS", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("HR_ACCOUNTING_OUTBOX", "FK_HR_HANDOFF_OUTBOX", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_INBOX", "FK_ACC_HANDOFF_INBOX", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_JOURNAL_LINK", "FK_ACC_HANDOFF_JOURNAL", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_JOURNAL_LINK", "FK_ACC_HANDOFF_JV", "VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("ACC_HANDOFF_POSTING_RECEIPT", "FK_ACC_HANDOFF_RECEIPT", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_HANDOFF_POSTING_RECEIPT", "FK_ACC_RECEIPT_JV", "VOUCHER_ID", "GL_VOUCHER_HEADER", "ID"),
                ("ACC_HANDOFF_PAYMENT", "FK_ACC_HANDOFF_PAYMENT", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID"),
                ("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_LOAN_FUND_RECEIPT", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID")
            };
            var installerParentTables = new HashSet<string>(StringComparer.Ordinal)
            {
                "HR_ACCOUNTING_HANDOFF"
            };
            foreach (var parentTable in foreignKeys.Select(x => x.RefTable).Distinct(StringComparer.Ordinal))
            {
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{parentTable}'") == 0)
                {
                    if (installerParentTables.Contains(parentTable)) continue;
                    throw new InvalidOperationException($"Required FK parent DEV_TEMPLATE.{parentTable} is missing; handoff install stopped before DDL.");
                }

                var parentIdColumn = await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{parentTable}' AND COLUMN_NAME='ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='N'");
                if (parentIdColumn != 1)
                    throw new InvalidOperationException($"FK parent DEV_TEMPLATE.{parentTable}.ID is not a required NUMBER(19,0); handoff install stopped before DDL.");

                var parentKey = await Count($"""
                    SELECT COUNT(*) FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='{parentTable}'
                      AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc
                           WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc
                                  WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME
                                    AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                                    AND cc.COLUMN_NAME='ID' AND cc.POSITION=1)
                    """);
                if (parentKey == 0)
                    throw new InvalidOperationException($"FK parent DEV_TEMPLATE.{parentTable}.ID lacks an enabled, validated primary/unique key; handoff install stopped before DDL.");
            }
            foreach (var (table, name, column, refTable, refColumn) in foreignKeys)
            {
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'") == 0) continue;
                var existing = await ReadConstraint(table, name);
                if (existing is not null && existing.Value.Type == "R" && existing.Value.Status == "ENABLED" &&
                    existing.Value.Validated == "VALIDATED" && existing.Value.Columns == column &&
                    existing.Value.ReferencedOwner == "DEV_TEMPLATE" &&
                    existing.Value.ReferencedTable == refTable && existing.Value.ReferencedColumns == refColumn) continue;
                if (await Count($"SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}' AND COLUMN_NAME='{column}'") == 0) continue;
                var childRows = await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.{table} c WHERE c.{column} IS NOT NULL");
                if (childRows == 0) continue;
                if (await Count($"SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{refTable}'") == 0)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} has values but referenced table {refTable} is absent; handoff install stopped before DDL.");
                var orphans = await Count($"SELECT COUNT(*) FROM DEV_TEMPLATE.{table} c WHERE c.{column} IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DEV_TEMPLATE.{refTable} p WHERE p.{refColumn}=c.{column})");
                if (orphans != 0)
                    throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} contains {orphans} values without a parent in {refTable}.{refColumn}; handoff install stopped before DDL.");
            }
            report.Add($"Preflighted existing handoff data for {uniquePlans.Length} unique keys and {foreignKeys.Length} foreign keys before DDL.");
        }
        await PreflightAddColumns();
        await PreflightNamedConstraints();
        await PreflightPrimaryKeysAndIndexes();
        var handoffTableDdls = new (string Table, string Ddl)[]
        {
            ("HR_ACCOUNTING_HANDOFF", "CREATE TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, COMPANY_ID NUMBER(19) NOT NULL, BRANCH_ID NUMBER(19) NULL, SOURCE_TYPE NVARCHAR2(20) NOT NULL, SOURCE_ID NVARCHAR2(100) NOT NULL, SOURCE_VERSION NUMBER(10) NOT NULL, OPERATION NVARCHAR2(20) NOT NULL, HANDOFF_REFERENCE NVARCHAR2(100) NOT NULL, PAYLOAD_HASH NVARCHAR2(64) NOT NULL, DELIVERY_STATUS NVARCHAR2(20) NOT NULL, CREATED_AT_UTC TIMESTAMP(7) NOT NULL, CREATED_BY NVARCHAR2(200) NOT NULL, CONSTRAINT UX_HR_HANDOFF_SOURCE UNIQUE (COMPANY_ID,SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION,OPERATION), CONSTRAINT UX_HR_HANDOFF_REF UNIQUE (COMPANY_ID,HANDOFF_REFERENCE))"),
            ("HR_ACCOUNTING_HANDOFF_LINE", "CREATE TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF_LINE (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL, LINE_TYPE NVARCHAR2(40) NOT NULL, CURRENCY_CODE NVARCHAR2(20) NOT NULL, AMOUNT NUMBER(18,4) NOT NULL, BRANCH_ID NUMBER(19) NULL, COST_CENTER_REFERENCE NVARCHAR2(100) NULL, SOURCE_REFERENCE NVARCHAR2(200) NULL, CONSTRAINT FK_HR_HANDOFF_LINE FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID))"),
            ("HR_ACCOUNTING_HANDOFF_TRANSITION", "CREATE TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF_TRANSITION (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL, FROM_STATUS NVARCHAR2(20) NOT NULL, TO_STATUS NVARCHAR2(20) NOT NULL, ACTOR NVARCHAR2(200) NOT NULL, REASON NVARCHAR2(1000) NULL, OCCURRED_AT_UTC TIMESTAMP(7) NOT NULL, CONSTRAINT FK_HR_HANDOFF_TRANS FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID))"),
            ("HR_ACCOUNTING_OUTBOX", "CREATE TABLE DEV_TEMPLATE.HR_ACCOUNTING_OUTBOX (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL, EVENT_KEY NVARCHAR2(160) NOT NULL UNIQUE, EVENT_TYPE NVARCHAR2(40) NOT NULL, PAYLOAD_HASH NVARCHAR2(64) NOT NULL, STATUS NVARCHAR2(20) NOT NULL, ATTEMPT_COUNT NUMBER(10) NOT NULL, CREATED_AT_UTC TIMESTAMP(7) NOT NULL, LAST_ATTEMPT_AT_UTC TIMESTAMP(7) NULL, LAST_ERROR NVARCHAR2(1000) NULL, CONSTRAINT FK_HR_HANDOFF_OUTBOX FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID))"),
            ("ACC_HANDOFF_INBOX", "CREATE TABLE DEV_TEMPLATE.ACC_HANDOFF_INBOX (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL, EVENT_KEY NVARCHAR2(160) NOT NULL UNIQUE, PAYLOAD_HASH NVARCHAR2(64) NOT NULL, RECEIVED_AT_UTC TIMESTAMP(7) NOT NULL, STATUS NVARCHAR2(20) NOT NULL, CONSTRAINT FK_ACC_HANDOFF_INBOX FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID))"),
            ("ACC_HANDOFF_JOURNAL_LINK", "CREATE TABLE DEV_TEMPLATE.ACC_HANDOFF_JOURNAL_LINK (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL UNIQUE, VOUCHER_ID NUMBER(19) NOT NULL UNIQUE, CREATED_AT_UTC TIMESTAMP(7) NOT NULL, CREATED_BY NVARCHAR2(200) NOT NULL, CONSTRAINT FK_ACC_HANDOFF_JOURNAL FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID), CONSTRAINT FK_ACC_HANDOFF_JV FOREIGN KEY (VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID))"),
            ("ACC_HANDOFF_POSTING_RECEIPT", "CREATE TABLE DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL UNIQUE, VOUCHER_ID NUMBER(19) NOT NULL, EVENT_KEY NVARCHAR2(160) NOT NULL UNIQUE, PAYLOAD_HASH NVARCHAR2(64) NOT NULL, RECEIVED_AT_UTC TIMESTAMP(7) NOT NULL, RECEIVED_BY NVARCHAR2(200) NOT NULL, SETTLEMENT_SNAPSHOT_JSON CLOB NULL, CONSTRAINT FK_ACC_HANDOFF_RECEIPT FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID), CONSTRAINT FK_ACC_RECEIPT_JV FOREIGN KEY (VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID))"),
            ("ACC_HANDOFF_PAYMENT", "CREATE TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL, PAYMENT_SEQUENCE NUMBER(10) NOT NULL, IDEMPOTENCY_KEY NVARCHAR2(160) NOT NULL, CURRENCY_CODE NVARCHAR2(20) NOT NULL, AMOUNT NUMBER(18,4) NOT NULL, METHOD NVARCHAR2(20) NOT NULL, PAYMENT_METHOD_CODE NVARCHAR2(50) NULL, STATUS NVARCHAR2(20) NOT NULL, EXTERNAL_REFERENCE NVARCHAR2(200) NULL, EVIDENCE_REFERENCE NVARCHAR2(500) NULL, FAILURE_REASON NVARCHAR2(1000) NULL, REVERSAL_REASON NVARCHAR2(1000) NULL, REVERSAL_EXTERNAL_REFERENCE NVARCHAR2(200) NULL, REVERSAL_EVIDENCE_REFERENCE NVARCHAR2(500) NULL, CREATED_AT_UTC TIMESTAMP(7) NOT NULL, CREATED_BY NVARCHAR2(200) NOT NULL, CONFIRMED_AT_UTC TIMESTAMP(7) NULL, CONFIRMED_BY NVARCHAR2(200) NULL, FAILED_AT_UTC TIMESTAMP(7) NULL, FAILED_BY NVARCHAR2(200) NULL, REVERSED_AT_UTC TIMESTAMP(7) NULL, REVERSED_BY NVARCHAR2(200) NULL, REVERSAL_VOUCHER_ID NUMBER(19) NULL, CONSTRAINT FK_ACC_HANDOFF_PAYMENT FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID), CONSTRAINT UX_ACC_HANDOFF_PAY_KEY UNIQUE (HANDOFF_ID,IDEMPOTENCY_KEY), CONSTRAINT UX_ACC_HANDOFF_PAY_SEQ UNIQUE (HANDOFF_ID,PAYMENT_SEQUENCE), CONSTRAINT UX_ACC_HANDOFF_PAY_REF UNIQUE (EXTERNAL_REFERENCE))"),
            ("ACC_LOAN_FUNDING_RECEIPT", "CREATE TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT (ID NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY PRIMARY KEY, HANDOFF_ID NUMBER(19) NOT NULL, SOURCE_TYPE NVARCHAR2(20) NOT NULL, SOURCE_ID NVARCHAR2(100) NOT NULL, SOURCE_VERSION NUMBER(10) DEFAULT 1 NOT NULL, FUNDED_AMOUNT NUMBER(18,4) NOT NULL, CURRENCY_CODE NVARCHAR2(20) NOT NULL, FUNDING_VOUCHER_ID NUMBER(19) NULL, REVERSAL_VOUCHER_ID NUMBER(19) NULL, EVENT_KEY NVARCHAR2(160) NOT NULL, EXTERNAL_REFERENCE NVARCHAR2(200) NOT NULL, REVERSAL_EVENT_KEY NVARCHAR2(160) NULL, REVERSAL_REASON NVARCHAR2(1000) NULL, REVERSAL_EXTERNAL_REFERENCE NVARCHAR2(200) NULL, REVERSAL_EVIDENCE_REFERENCE NVARCHAR2(500) NULL, RECEIVED_AT_UTC TIMESTAMP(7) NOT NULL, RECEIVED_BY NVARCHAR2(200) NOT NULL, REVERSED_AT_UTC TIMESTAMP(7) NULL, REVERSED_BY NVARCHAR2(200) NULL, CONSTRAINT FK_ACC_LOAN_FUND_RECEIPT FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID), CONSTRAINT FK_ACC_FUNDING_GL_VOUCHER FOREIGN KEY (FUNDING_VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID), CONSTRAINT FK_ACC_FUND_REV_GL_VCH FOREIGN KEY (REVERSAL_VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID), CONSTRAINT UX_ACC_FUND_HANDOFF UNIQUE (HANDOFF_ID), CONSTRAINT UX_ACC_FUND_EVENT UNIQUE (EVENT_KEY), CONSTRAINT UX_ACC_FUND_SRCVER UNIQUE (SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION), CONSTRAINT UX_ACC_FUND_REF UNIQUE (EXTERNAL_REFERENCE), CONSTRAINT UX_ACC_FUND_VOUCHER UNIQUE (FUNDING_VOUCHER_ID), CONSTRAINT UX_ACC_FUND_REV_EVENT UNIQUE (REVERSAL_EVENT_KEY), CONSTRAINT UX_ACC_FUND_REV_REF UNIQUE (REVERSAL_EXTERNAL_REFERENCE), CONSTRAINT UX_ACC_FUND_REV_VCH UNIQUE (REVERSAL_VOUCHER_ID))"),
        };

        await PreflightHandoffTableColumns(handoffTableDdls);
        await PreflightConstraintData();
        foreach (var (table, ddl) in handoffTableDdls)
            await Run(table, ddl);








        await AddColumn("HR_EMPLOYEE_LOAN", "CURRENCY_CODE", "ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE_LOAN ADD (CURRENCY_CODE NVARCHAR2(20) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=20 AND NULLABLE='Y' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'");
        await AddColumn("HR_EMPLOYEE_ADVANCE", "CURRENCY_CODE", "ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE_ADVANCE ADD (CURRENCY_CODE NVARCHAR2(20) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=20 AND NULLABLE='Y' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'");
        await AddColumn("HR_EMPLOYEE_ADVANCE", "RESERVED_BY_HANDOFF_ID", "ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE_ADVANCE ADD (RESERVED_BY_HANDOFF_ID NUMBER(19) NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'");
        await AddColumn("HR_LOAN_REPAYMENT_SCHEDULE", "RESERVED_BY_HANDOFF_ID", "ALTER TABLE DEV_TEMPLATE.HR_LOAN_REPAYMENT_SCHEDULE ADD (RESERVED_BY_HANDOFF_ID NUMBER(19) NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'");
        await AddColumn("HR_ACCOUNTING_HANDOFF", "PAY_PERIOD", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF ADD (PAY_PERIOD NVARCHAR2(40) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=40 AND NULLABLE='Y'");
        await AddColumn("HR_ACCOUNTING_HANDOFF", "SOURCE_APPROVED_AT_UTC", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF ADD (SOURCE_APPROVED_AT_UTC TIMESTAMP(7) NULL)", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "PAYMENT_METHOD_CODE", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (PAYMENT_METHOD_CODE NVARCHAR2(50) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=50 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "FAILURE_REASON", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (FAILURE_REASON NVARCHAR2(1000) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "FAILED_AT_UTC", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (FAILED_AT_UTC TIMESTAMP(7) NULL)", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "FAILED_BY", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (FAILED_BY NVARCHAR2(200) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "REVERSAL_REASON", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (REVERSAL_REASON NVARCHAR2(1000) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "REVERSAL_EXTERNAL_REFERENCE", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (REVERSAL_EXTERNAL_REFERENCE NVARCHAR2(200) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "REVERSAL_EVIDENCE_REFERENCE", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (REVERSAL_EVIDENCE_REFERENCE NVARCHAR2(500) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=500 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "REVERSED_AT_UTC", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (REVERSED_AT_UTC TIMESTAMP(7) NULL)", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "REVERSED_BY", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (REVERSED_BY NVARCHAR2(200) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_PAYMENT", "REVERSAL_VOUCHER_ID", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD (REVERSAL_VOUCHER_ID NUMBER(19) NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'");
        await AddColumn("ACC_HANDOFF_POSTING_RECEIPT", "SETTLEMENT_SNAPSHOT_JSON", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT ADD (SETTLEMENT_SNAPSHOT_JSON CLOB NULL)", "DATA_TYPE='CLOB' AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "FUNDING_VOUCHER_ID", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (FUNDING_VOUCHER_ID NUMBER(19) NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "SOURCE_VERSION", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (SOURCE_VERSION NUMBER(10) DEFAULT 1 NOT NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=10 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='N'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_VOUCHER_ID", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (REVERSAL_VOUCHER_ID NUMBER(19) NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_EVENT_KEY", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (REVERSAL_EVENT_KEY NVARCHAR2(160) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=160 AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_REASON", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (REVERSAL_REASON NVARCHAR2(1000) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_EXTERNAL_REFERENCE", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (REVERSAL_EXTERNAL_REFERENCE NVARCHAR2(200) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "REVERSAL_EVIDENCE_REFERENCE", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (REVERSAL_EVIDENCE_REFERENCE NVARCHAR2(500) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=500 AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "REVERSED_AT_UTC", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (REVERSED_AT_UTC TIMESTAMP(7) NULL)", "DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y'");
        await AddColumn("ACC_LOAN_FUNDING_RECEIPT", "REVERSED_BY", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD (REVERSED_BY NVARCHAR2(200) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y'");
        await AddColumn("HR_PAYROLL_RUN", "CORRECTION_OF_RUN_ID", "ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN ADD (CORRECTION_OF_RUN_ID NUMBER(19) NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'");
        await AddColumn("HR_PAYROLL_RUN", "CORRECTION_ROOT_RUN_ID", "ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN ADD (CORRECTION_ROOT_RUN_ID NUMBER(19) NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='Y'");
        await AddColumn("HR_PAYROLL_RUN", "CORRECTION_VERSION", "ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN ADD (CORRECTION_VERSION NUMBER(10) DEFAULT 1 NOT NULL)", "DATA_TYPE='NUMBER' AND DATA_PRECISION=10 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='N'");
        await AddColumn("HR_PAYROLL_RUN", "CORRECTION_REASON", "ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN ADD (CORRECTION_REASON NVARCHAR2(1000) NULL)", "DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=1000 AND NULLABLE='Y'");
        await EnsureConstraint("HR_PAYROLL_RUN", "FK_HR_PAYRUN_CORR_OF", "ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN ADD CONSTRAINT FK_HR_PAYRUN_CORR_OF FOREIGN KEY (CORRECTION_OF_RUN_ID) REFERENCES DEV_TEMPLATE.HR_PAYROLL_RUN(ID)", "R", "CORRECTION_OF_RUN_ID", "HR_PAYROLL_RUN", "ID");
        await EnsureConstraint("HR_PAYROLL_RUN", "FK_HR_PAYRUN_CORR_ROOT", "ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_RUN ADD CONSTRAINT FK_HR_PAYRUN_CORR_ROOT FOREIGN KEY (CORRECTION_ROOT_RUN_ID) REFERENCES DEV_TEMPLATE.HR_PAYROLL_RUN(ID)", "R", "CORRECTION_ROOT_RUN_ID", "HR_PAYROLL_RUN", "ID");
        await HrPayrollCorrectionIndexSchema.Install(connection, report);
        await DropConstraintIfPresent("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_SOURCE", "U", "SOURCE_TYPE,SOURCE_ID");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_SRCVER", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_SRCVER UNIQUE (SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION)", "U", "SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_FUNDING_GL_VOUCHER", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT FK_ACC_FUNDING_GL_VOUCHER FOREIGN KEY (FUNDING_VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID)", "R", "FUNDING_VOUCHER_ID", "GL_VOUCHER_HEADER", "ID");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_VOUCHER", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_VOUCHER UNIQUE (FUNDING_VOUCHER_ID)", "U", "FUNDING_VOUCHER_ID");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_FUND_REV_GL_VCH", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT FK_ACC_FUND_REV_GL_VCH FOREIGN KEY (REVERSAL_VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID)", "R", "REVERSAL_VOUCHER_ID", "GL_VOUCHER_HEADER", "ID");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_EVENT", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_REV_EVENT UNIQUE (REVERSAL_EVENT_KEY)", "U", "REVERSAL_EVENT_KEY");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_REF", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_REV_REF UNIQUE (REVERSAL_EXTERNAL_REFERENCE)", "U", "REVERSAL_EXTERNAL_REFERENCE");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REV_VCH", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_REV_VCH UNIQUE (REVERSAL_VOUCHER_ID)", "U", "REVERSAL_VOUCHER_ID");

        await EnsureConstraint("HR_ACCOUNTING_HANDOFF", "UX_HR_HANDOFF_SOURCE", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF ADD CONSTRAINT UX_HR_HANDOFF_SOURCE UNIQUE (COMPANY_ID,SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION,OPERATION)", "U", "COMPANY_ID,SOURCE_TYPE,SOURCE_ID,SOURCE_VERSION,OPERATION");
        await EnsureConstraint("HR_ACCOUNTING_HANDOFF", "UX_HR_HANDOFF_REF", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF ADD CONSTRAINT UX_HR_HANDOFF_REF UNIQUE (COMPANY_ID,HANDOFF_REFERENCE)", "U", "COMPANY_ID,HANDOFF_REFERENCE");
        await EnsureConstraint("HR_ACCOUNTING_HANDOFF_LINE", "FK_HR_HANDOFF_LINE", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF_LINE ADD CONSTRAINT FK_HR_HANDOFF_LINE FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("HR_ACCOUNTING_HANDOFF_TRANSITION", "FK_HR_HANDOFF_TRANS", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF_TRANSITION ADD CONSTRAINT FK_HR_HANDOFF_TRANS FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("HR_ACCOUNTING_OUTBOX", "FK_HR_HANDOFF_OUTBOX", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_OUTBOX ADD CONSTRAINT FK_HR_HANDOFF_OUTBOX FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("ACC_HANDOFF_INBOX", "FK_ACC_HANDOFF_INBOX", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_INBOX ADD CONSTRAINT FK_ACC_HANDOFF_INBOX FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("ACC_HANDOFF_JOURNAL_LINK", "FK_ACC_HANDOFF_JOURNAL", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_JOURNAL_LINK ADD CONSTRAINT FK_ACC_HANDOFF_JOURNAL FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("ACC_HANDOFF_JOURNAL_LINK", "FK_ACC_HANDOFF_JV", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_JOURNAL_LINK ADD CONSTRAINT FK_ACC_HANDOFF_JV FOREIGN KEY (VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID)", "R", "VOUCHER_ID", "GL_VOUCHER_HEADER", "ID");
        await EnsureConstraint("ACC_HANDOFF_POSTING_RECEIPT", "FK_ACC_HANDOFF_RECEIPT", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT ADD CONSTRAINT FK_ACC_HANDOFF_RECEIPT FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("ACC_HANDOFF_POSTING_RECEIPT", "FK_ACC_RECEIPT_JV", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT ADD CONSTRAINT FK_ACC_RECEIPT_JV FOREIGN KEY (VOUCHER_ID) REFERENCES DEV_TEMPLATE.GL_VOUCHER_HEADER(ID)", "R", "VOUCHER_ID", "GL_VOUCHER_HEADER", "ID");
        await EnsureConstraint("ACC_HANDOFF_PAYMENT", "FK_ACC_HANDOFF_PAYMENT", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD CONSTRAINT FK_ACC_HANDOFF_PAYMENT FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_KEY", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD CONSTRAINT UX_ACC_HANDOFF_PAY_KEY UNIQUE (HANDOFF_ID,IDEMPOTENCY_KEY)", "U", "HANDOFF_ID,IDEMPOTENCY_KEY");
        await EnsureConstraint("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_SEQ", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD CONSTRAINT UX_ACC_HANDOFF_PAY_SEQ UNIQUE (HANDOFF_ID,PAYMENT_SEQUENCE)", "U", "HANDOFF_ID,PAYMENT_SEQUENCE");
        await EnsureConstraint("ACC_HANDOFF_PAYMENT", "UX_ACC_HANDOFF_PAY_REF", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_PAYMENT ADD CONSTRAINT UX_ACC_HANDOFF_PAY_REF UNIQUE (EXTERNAL_REFERENCE)", "U", "EXTERNAL_REFERENCE");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "FK_ACC_LOAN_FUND_RECEIPT", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT FK_ACC_LOAN_FUND_RECEIPT FOREIGN KEY (HANDOFF_ID) REFERENCES DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF(ID)", "R", "HANDOFF_ID", "HR_ACCOUNTING_HANDOFF", "ID");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_HANDOFF", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_HANDOFF UNIQUE (HANDOFF_ID)", "U", "HANDOFF_ID");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_EVENT", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_EVENT UNIQUE (EVENT_KEY)", "U", "EVENT_KEY");
        await EnsureConstraint("ACC_LOAN_FUNDING_RECEIPT", "UX_ACC_FUND_REF", "ALTER TABLE DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT ADD CONSTRAINT UX_ACC_FUND_REF UNIQUE (EXTERNAL_REFERENCE)", "U", "EXTERNAL_REFERENCE");

        await EnsureUniqueColumns("HR_ACCOUNTING_OUTBOX", "EVENT_KEY", "UX_HR_OUTBOX_EVENT", "ALTER TABLE DEV_TEMPLATE.HR_ACCOUNTING_OUTBOX ADD CONSTRAINT UX_HR_OUTBOX_EVENT UNIQUE (EVENT_KEY)");
        await EnsureUniqueColumns("ACC_HANDOFF_INBOX", "EVENT_KEY", "UX_ACC_INBOX_EVENT", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_INBOX ADD CONSTRAINT UX_ACC_INBOX_EVENT UNIQUE (EVENT_KEY)");
        await EnsureUniqueColumns("ACC_HANDOFF_JOURNAL_LINK", "HANDOFF_ID", "UX_ACC_JOURNAL_HANDOFF", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_JOURNAL_LINK ADD CONSTRAINT UX_ACC_JOURNAL_HANDOFF UNIQUE (HANDOFF_ID)");
        await EnsureUniqueColumns("ACC_HANDOFF_JOURNAL_LINK", "VOUCHER_ID", "UX_ACC_JOURNAL_VOUCHER", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_JOURNAL_LINK ADD CONSTRAINT UX_ACC_JOURNAL_VOUCHER UNIQUE (VOUCHER_ID)");
        await EnsureUniqueColumns("ACC_HANDOFF_POSTING_RECEIPT", "HANDOFF_ID", "UX_ACC_RECEIPT_HANDOFF", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT ADD CONSTRAINT UX_ACC_RECEIPT_HANDOFF UNIQUE (HANDOFF_ID)");
        await EnsureUniqueColumns("ACC_HANDOFF_POSTING_RECEIPT", "EVENT_KEY", "UX_ACC_RECEIPT_EVENT", "ALTER TABLE DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT ADD CONSTRAINT UX_ACC_RECEIPT_EVENT UNIQUE (EVENT_KEY)");

        foreach (var (table, constraint) in new[]
        {
            ("HR_ACCOUNTING_HANDOFF", "PK_HR_HANDOFF"),
            ("HR_ACCOUNTING_HANDOFF_LINE", "PK_HR_HANDOFF_LINE"),
            ("HR_ACCOUNTING_HANDOFF_TRANSITION", "PK_HR_HANDOFF_TRANS"),
            ("HR_ACCOUNTING_OUTBOX", "PK_HR_HANDOFF_OUTBOX"),
            ("ACC_HANDOFF_INBOX", "PK_ACC_HANDOFF_INBOX"),
            ("ACC_HANDOFF_JOURNAL_LINK", "PK_ACC_HANDOFF_JOURNAL"),
            ("ACC_HANDOFF_POSTING_RECEIPT", "PK_ACC_HANDOFF_RECEIPT"),
            ("ACC_HANDOFF_PAYMENT", "PK_ACC_HANDOFF_PAYMENT"),
            ("ACC_LOAN_FUNDING_RECEIPT", "PK_ACC_FUNDING_RECEIPT")
        })
            await EnsurePrimaryKey(table, constraint);

        await EnsureIndex("HR_ACCOUNTING_HANDOFF_LINE", "IX_HR_HANDOFF_LINE", "HANDOFF_ID", "CREATE INDEX DEV_TEMPLATE.IX_HR_HANDOFF_LINE ON DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF_LINE(HANDOFF_ID)");
        await EnsureIndex("HR_ACCOUNTING_HANDOFF_TRANSITION", "IX_HR_HANDOFF_TRANS", "HANDOFF_ID", "CREATE INDEX DEV_TEMPLATE.IX_HR_HANDOFF_TRANS ON DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF_TRANSITION(HANDOFF_ID)");
        await EnsureIndex("HR_ACCOUNTING_OUTBOX", "IX_HR_HANDOFF_OUTBOX", "STATUS,CREATED_AT_UTC", "CREATE INDEX DEV_TEMPLATE.IX_HR_HANDOFF_OUTBOX ON DEV_TEMPLATE.HR_ACCOUNTING_OUTBOX(STATUS,CREATED_AT_UTC)");
        await EnsureIndex("ACC_HANDOFF_INBOX", "IX_ACC_HANDOFF_INBOX", "HANDOFF_ID,RECEIVED_AT_UTC", "CREATE INDEX DEV_TEMPLATE.IX_ACC_HANDOFF_INBOX ON DEV_TEMPLATE.ACC_HANDOFF_INBOX(HANDOFF_ID,RECEIVED_AT_UTC)");

        foreach (var table in new[] { "HR_ACCOUNTING_HANDOFF", "HR_ACCOUNTING_HANDOFF_LINE", "HR_ACCOUNTING_HANDOFF_TRANSITION", "HR_ACCOUNTING_OUTBOX", "ACC_HANDOFF_INBOX", "ACC_HANDOFF_JOURNAL_LINK", "ACC_HANDOFF_POSTING_RECEIPT", "ACC_HANDOFF_PAYMENT", "ACC_LOAN_FUNDING_RECEIPT" })
        {
            await using var mappedSelect = connection.CreateCommand();
            mappedSelect.CommandText = $"SELECT * FROM DEV_TEMPLATE.{table} WHERE 1=0";
            await using var reader = await mappedSelect.ExecuteReaderAsync();
            report.Add($"Verified mapped SELECT for DEV_TEMPLATE.{table}; no rows read.");
        }
    }
}
