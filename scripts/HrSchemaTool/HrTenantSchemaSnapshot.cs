using Oracle.ManagedDataAccess.Client;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class HrTenantSchemaSnapshot
{
    public static async Task WriteAsync(OracleConnection connection, string owner, string outputPath)
    {
        await using (var readOnly = connection.CreateCommand())
        {
            readOnly.CommandText = "SET TRANSACTION READ ONLY";
            await readOnly.ExecuteNonQueryAsync();

            var capturedAtUtc = DateTime.UtcNow;
            var report = new List<string>
            {
                $"HR tenant schema snapshot [owner]: {owner}",
                $"Captured at UTC: {capturedAtUtc:O}",
                "Read-only data dictionary snapshot; no row values or row counts were selected."
            };

            async Task<List<string[]>> QueryAsync(string sql)
            {
                await using var command = connection.CreateCommand();
                command.BindByName = true;
                command.CommandTimeout = 30;
                command.CommandText = sql;
                command.Parameters.Add("owner", OracleDbType.Varchar2, owner, System.Data.ParameterDirection.Input);
                await using var reader = await command.ExecuteReaderAsync();
                var rows = new List<string[]>();
                while (await reader.ReadAsync())
                    rows.Add(Enumerable.Range(0, reader.FieldCount)
                        .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()!)
                        .ToArray());
                return rows;
            }

            static List<Dictionary<string, string>> AsObjects(List<string[]> rows, params string[] fields) => rows
                .Select(row => fields.Select((field, index) => (field, value: row[index]))
                    .ToDictionary(item => item.field, item => item.value, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var row in await QueryAsync("SELECT :owner,SYS_CONTEXT('USERENV','SESSION_USER'),SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL"))
                report.Add("Oracle session [user,current-schema]: " + string.Join(" | ", row));

            var tables = (await QueryAsync("SELECT TABLE_NAME FROM ALL_TABLES WHERE OWNER=:owner AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME"))
                .Select(row => row[0])
                .ToArray();
            if (tables.Length == 0)
                throw new InvalidOperationException($"No HR tables are visible for schema owner {owner}; snapshot was not written.");
            report.Add($"Visible HR tables: {tables.Length}.");

            var columns = await QueryAsync("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,DATA_PRECISION,DATA_SCALE,CHAR_LENGTH,CHAR_USED,NULLABLE,DATA_DEFAULT,HIDDEN_COLUMN,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER=:owner AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME,INTERNAL_COLUMN_ID");
            foreach (var row in columns)
                report.Add("Column [table,column,type,bytes,precision,scale,chars,char-used,nullable,default,hidden,virtual]: " + string.Join(" | ", row));

            var identities = await QueryAsync("SELECT TABLE_NAME,COLUMN_NAME,GENERATION_TYPE,SEQUENCE_NAME FROM ALL_TAB_IDENTITY_COLS WHERE OWNER=:owner AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME,COLUMN_NAME");
            foreach (var row in identities)
                report.Add("Identity [table,column,generation,sequence]: " + string.Join(" | ", row));

            var constraints = await QueryAsync("SELECT c.TABLE_NAME,c.CONSTRAINT_NAME,c.CONSTRAINT_TYPE,c.STATUS,c.VALIDATED,c.DELETE_RULE,x.COLUMN_NAME,x.POSITION,c.R_OWNER,c.R_CONSTRAINT_NAME,r.TABLE_NAME,rc.COLUMN_NAME,c.SEARCH_CONDITION_VC FROM ALL_CONSTRAINTS c LEFT JOIN ALL_CONS_COLUMNS x ON x.OWNER=c.OWNER AND x.CONSTRAINT_NAME=c.CONSTRAINT_NAME LEFT JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME LEFT JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME AND rc.POSITION=x.POSITION WHERE c.OWNER=:owner AND c.TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY c.TABLE_NAME,c.CONSTRAINT_NAME,x.POSITION");
            foreach (var row in constraints)
                report.Add("Constraint [table,name,type,status,validated,delete,column,position,ref-owner,ref-key,ref-table,ref-column,check-expression-vc]: " + string.Join(" | ", row));

            var indexes = await QueryAsync("SELECT i.TABLE_NAME,i.INDEX_NAME,i.UNIQUENESS,i.STATUS,x.COLUMN_NAME,x.COLUMN_POSITION,e.COLUMN_EXPRESSION FROM ALL_INDEXES i JOIN ALL_IND_COLUMNS x ON x.INDEX_OWNER=i.OWNER AND x.INDEX_NAME=i.INDEX_NAME LEFT JOIN ALL_IND_EXPRESSIONS e ON e.INDEX_OWNER=x.INDEX_OWNER AND e.INDEX_NAME=x.INDEX_NAME AND e.COLUMN_POSITION=x.COLUMN_POSITION WHERE i.OWNER=:owner AND i.TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY i.TABLE_NAME,i.INDEX_NAME,x.COLUMN_POSITION");
            foreach (var row in indexes)
                report.Add("Index [table,name,uniqueness,status,column,position,expression]: " + string.Join(" | ", row));

            var triggers = await QueryAsync("SELECT TABLE_NAME,TRIGGER_NAME,STATUS,TRIGGERING_EVENT,TRIGGER_TYPE FROM ALL_TRIGGERS WHERE OWNER=:owner AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME,TRIGGER_NAME");
            foreach (var row in triggers)
                report.Add("Trigger [table,name,status,event,type]: " + string.Join(" | ", row));

            // Hash source instead of placing PL/SQL text in portable snapshots. This
            // detects same-name trigger body drift without disclosing implementation text.
            var triggerSourceRows = await QueryAsync("SELECT t.TABLE_NAME,t.TRIGGER_NAME,s.LINE,s.TEXT FROM ALL_TRIGGERS t JOIN ALL_SOURCE s ON s.OWNER=t.OWNER AND s.NAME=t.TRIGGER_NAME AND s.TYPE='TRIGGER' WHERE t.OWNER=:owner AND t.TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY t.TABLE_NAME,t.TRIGGER_NAME,s.LINE");
            var triggerSourceHashes = triggerSourceRows
                .GroupBy(row => (Table: row[0], Name: row[1]))
                .Select(group => new
                {
                    table = group.Key.Table,
                    name = group.Key.Name,
                    lineCount = group.Count(),
                    sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", group.Select(row => row[3])))))
                })
                .OrderBy(row => row.table, StringComparer.Ordinal)
                .ThenBy(row => row.name, StringComparer.Ordinal)
                .ToArray();
            foreach (var row in triggerSourceHashes)
                report.Add($"Trigger source hash [table,name,lines,sha256]: {row.table} | {row.name} | {row.lineCount} | {row.sha256}");

            var triggerErrors = await QueryAsync("SELECT NAME,TYPE,LINE,POSITION,TEXT FROM ALL_ERRORS WHERE OWNER=:owner AND TYPE='TRIGGER' AND NAME IN (SELECT TRIGGER_NAME FROM ALL_TRIGGERS WHERE OWNER=:owner AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\') ORDER BY NAME,SEQUENCE");
            foreach (var row in triggerErrors)
                report.Add("Trigger error [name,type,line,position,text]: " + string.Join(" | ", row));

            var foreignTriggers = await QueryAsync("SELECT OWNER,TABLE_NAME,TRIGGER_NAME,STATUS,TRIGGERING_EVENT,TRIGGER_TYPE FROM ALL_TRIGGERS WHERE TABLE_OWNER=:owner AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' AND OWNER<>:owner ORDER BY TABLE_NAME,OWNER,TRIGGER_NAME");
            foreach (var row in foreignTriggers)
                report.Add("Foreign trigger attached to tenant HR table [owner,table-owner,table,trigger,status,event,type]: " + string.Join(" | ", row));

            var dependencies = await QueryAsync("SELECT REFERENCED_NAME,OWNER,NAME,TYPE,DEPENDENCY_TYPE FROM ALL_DEPENDENCIES WHERE OWNER=:owner AND REFERENCED_OWNER=:owner AND REFERENCED_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY REFERENCED_NAME,OWNER,TYPE,NAME");
            foreach (var row in dependencies)
                report.Add("HR static dependency [table,owner,name,type,dependency]: " + string.Join(" | ", row));

            report.Add("Dependency scan is limited to visible static ALL_DEPENDENCIES rows in this schema; dynamic SQL and external integrations are not included.");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllLinesAsync(outputPath, report);
            var snapshot = new
            {
                SnapshotVersion = 1,
                Owner = owner,
                CapturedAtUtc = capturedAtUtc,
                Tables = tables,
                Columns = AsObjects(columns, "table", "column", "dataType", "dataLengthBytes", "precision", "scale", "charLength", "charUsed", "nullable", "default", "hidden", "virtual"),
                Identities = AsObjects(identities, "table", "column", "generationType", "sequenceName"),
                Constraints = AsObjects(constraints, "table", "name", "type", "status", "validated", "deleteRule", "column", "position", "referencedOwner", "referencedConstraint", "referencedTable", "referencedColumn", "searchCondition"),
                Indexes = AsObjects(indexes, "table", "name", "uniqueness", "status", "column", "position", "expression"),
                Triggers = AsObjects(triggers, "table", "name", "status", "events", "type"),
                TriggerSourceHashes = triggerSourceHashes,
                TriggerErrors = AsObjects(triggerErrors, "name", "type", "line", "position", "text"),
                ForeignTriggers = AsObjects(foreignTriggers, "owner", "table", "name", "status", "events", "type"),
                StaticDependencies = AsObjects(dependencies, "referencedTable", "owner", "name", "type", "dependencyType"),
                Limitations = new[] { "No business row values or row counts were selected.", "Trigger source is represented by SHA-256 and line count only; source text is not exported.", "Dependencies include only visible static ALL_DEPENDENCIES rows; dynamic SQL and external integrations are not included.", "CHECK expressions use Oracle SEARCH_CONDITION_VC; confirm any expression unavailable/truncated in that view separately." }
            };
            await File.WriteAllTextAsync(Path.ChangeExtension(outputPath, ".json"),
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
