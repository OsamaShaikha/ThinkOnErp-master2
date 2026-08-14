using System.Data;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Accounting;

public sealed class DevTemplateDbExecutor
{
    private const string ConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1539))(CONNECT_DATA=(SERVICE_NAME=free)));User Id=THINKON_ERP;Password=thinkon_erp;Pooling=false;";

    [Fact]
    public async Task ApplyCoaToDevTemplateDatabase()
    {
        Console.WriteLine("Connecting to Oracle Database at 178.104.126.99:1539...");

        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        Console.WriteLine("Connection opened successfully.");

        var ddlStatements = new[]
        {
            "BEGIN EXECUTE IMMEDIATE 'DROP TABLE \"DEV_TEMPLATE\".\"GL_ACCOUNT_BRANCH\" CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN NULL; ELSE RAISE; END IF; END;",
            "BEGIN EXECUTE IMMEDIATE 'DROP TABLE \"DEV_TEMPLATE\".\"GL_ACCOUNT\" CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN NULL; ELSE RAISE; END IF; END;",
            @"
            CREATE TABLE ""DEV_TEMPLATE"".""GL_ACCOUNT"" (
                ""ACCOUNT_CODE"" NVARCHAR2(50) NOT NULL,
                ""ACCOUNT_NAME_AR"" NVARCHAR2(200) NOT NULL,
                ""ACCOUNT_NAME_EN"" NVARCHAR2(200) NOT NULL,
                ""PARENT_ACCOUNT_CODE"" NVARCHAR2(50) NULL,
                ""ACCOUNT_LEVEL"" NUMBER(2) NOT NULL,
                ""ACCOUNT_TYPE"" NVARCHAR2(10) NOT NULL,
                ""NORMAL_BALANCE"" NVARCHAR2(1) NOT NULL,
                ""IS_CONTRA"" NUMBER(1) DEFAULT 0 NOT NULL,
                ""IS_CONTROL_ACCOUNT"" NUMBER(1) DEFAULT 0 NOT NULL,
                ""CONTROL_ACCOUNT_TYPE"" NVARCHAR2(20) NULL,
                ""IS_BRANCH_SPECIFIC"" NUMBER(1) DEFAULT 0 NOT NULL,
                ""IS_CLEARING"" NUMBER(1) DEFAULT 0 NOT NULL,
                ""IS_ACTIVE"" NUMBER(1) DEFAULT 1 NOT NULL,
                ""DESCRIPTION"" NVARCHAR2(1000) NULL,
                ""NOTES"" NVARCHAR2(2000) NULL,
                CONSTRAINT ""PK_GL_ACC"" PRIMARY KEY (""ACCOUNT_CODE""),
                CONSTRAINT ""FK_GL_ACC_PARENT"" FOREIGN KEY (""PARENT_ACCOUNT_CODE"") REFERENCES ""DEV_TEMPLATE"".""GL_ACCOUNT"" (""ACCOUNT_CODE""),
                CONSTRAINT ""CK_GL_ACCOUNT_LEVEL"" CHECK (""ACCOUNT_LEVEL"" BETWEEN 1 AND 5),
                CONSTRAINT ""CK_GL_ACCOUNT_TYPE"" CHECK (""ACCOUNT_TYPE"" IN ('HEADER', 'DETAIL')),
                CONSTRAINT ""CK_GL_ACCOUNT_BALANCE"" CHECK (""NORMAL_BALANCE"" IN ('D', 'C')),
                CONSTRAINT ""CK_GL_ACCOUNT_FLAGS"" CHECK (""IS_CONTRA"" IN (0, 1) AND ""IS_CONTROL_ACCOUNT"" IN (0, 1) AND ""IS_BRANCH_SPECIFIC"" IN (0, 1) AND ""IS_CLEARING"" IN (0, 1) AND ""IS_ACTIVE"" IN (0, 1)),
                CONSTRAINT ""CK_GL_ACCOUNT_CONTROL_TYPE"" CHECK ((""IS_CONTROL_ACCOUNT"" = 0 AND ""CONTROL_ACCOUNT_TYPE"" IS NULL) OR (""IS_CONTROL_ACCOUNT"" = 1 AND ""CONTROL_ACCOUNT_TYPE"" IN ('AR', 'AP', 'INVENTORY')))
            )",
            @"
            CREATE TABLE ""DEV_TEMPLATE"".""GL_ACCOUNT_BRANCH"" (
                ""ACCOUNT_CODE"" NVARCHAR2(50) NOT NULL,
                ""BRANCH_ID"" NUMBER(19) NOT NULL,
                ""IS_ACTIVE"" NUMBER(1) DEFAULT 1 NOT NULL,
                CONSTRAINT ""PK_GL_ACC_BRANCH"" PRIMARY KEY (""ACCOUNT_CODE"", ""BRANCH_ID""),
                CONSTRAINT ""FK_GLAB_ACC"" FOREIGN KEY (""ACCOUNT_CODE"") REFERENCES ""DEV_TEMPLATE"".""GL_ACCOUNT"" (""ACCOUNT_CODE""),
                CONSTRAINT ""CK_GL_ACC_BRANCH_ACTIVE"" CHECK (""IS_ACTIVE"" IN (0, 1))
            )",
            "CREATE INDEX \"DEV_TEMPLATE\".\"IX_GL_ACCOUNT_PARENT\" ON \"DEV_TEMPLATE\".\"GL_ACCOUNT\" (\"PARENT_ACCOUNT_CODE\")",
            "CREATE INDEX \"DEV_TEMPLATE\".\"IX_GL_ACCOUNT_CTRL\" ON \"DEV_TEMPLATE\".\"GL_ACCOUNT\" (\"IS_CONTROL_ACCOUNT\")",
            "CREATE INDEX \"DEV_TEMPLATE\".\"IX_GL_ACC_BRANCH_BRANCH\" ON \"DEV_TEMPLATE\".\"GL_ACCOUNT_BRANCH\" (\"BRANCH_ID\")"
        };

        foreach (var sql in ddlStatements)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            try
            {
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("Executed DDL statement successfully.");
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"DDL Warning/Error: {ex.Message}");
            }
        }

        // Read and execute 93_Seed_DEV_TEMPLATE_COA.sql script
        var seedScriptPath = @"D:\ThinkOnErp\Database\Scripts\93_Seed_DEV_TEMPLATE_COA.sql";
        if (File.Exists(seedScriptPath))
        {
            var content = await File.ReadAllTextAsync(seedScriptPath);
            var lines = content.Split('\n');
            int inserted = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = trimmed.TrimEnd(';');
                    await cmd.ExecuteNonQueryAsync();
                    inserted++;
                }
            }

            Console.WriteLine($"Successfully inserted {inserted} GL_ACCOUNT records into DEV_TEMPLATE schema!");
        }

        // Verify count
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM \"DEV_TEMPLATE\".\"GL_ACCOUNT\"";
            var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
            Console.WriteLine($"VERIFICATION: DEV_TEMPLATE.GL_ACCOUNT currently contains {count} records.");
        }
    }
}
