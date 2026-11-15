using Oracle.ManagedDataAccess.Client;
internal static class HrBranchLocationSchema
{
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_BRANCH_ATTENDANCE_LOCATION", "BRANCH_ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "COMPANY_ID", "NUMBER", "N", Precision: 19, Scale: 0),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "LATITUDE", "NUMBER", "Y", Precision: 10, Scale: 7),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "LONGITUDE", "NUMBER", "Y", Precision: 11, Scale: 7),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "RADIUS_METERS", "NUMBER", "N", Precision: 10, Scale: 2),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "ENFORCE_GEOFENCE", "NUMBER", "N", Precision: 1, Scale: 0),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "CREATION_USER", "NVARCHAR2", "N", CharacterLength: 200),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "CREATION_DATE", "TIMESTAMP(7)", "N"),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "UPDATE_USER", "NVARCHAR2", "Y", CharacterLength: 200),
        new("HR_BRANCH_ATTENDANCE_LOCATION", "UPDATE_DATE", "TIMESTAMP(7)", "Y")
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false,
            allowMissingTables: new HashSet<string>(["HR_BRANCH_ATTENDANCE_LOCATION"], StringComparer.Ordinal));
        await VerifyBranchParentKey(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_BRANCH_ATTENDANCE_LOCATION'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
        {
            await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
            await VerifyConstraints(connection);
            report.Add("Branch location table already exists and matches the mapped scalar column metadata; no settings changed.");
            return;
        }

        foreach (var name in new[] { "FK_HR_BRANCH_LOCATION", "CK_HR_BRANCH_LOCATION" })
        {
            command.Parameters.Clear();
            command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME=:constraintName";
            command.Parameters.Add(new OracleParameter("constraintName", name));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException($"Constraint name {name} is already used in DEV_TEMPLATE; branch-location DDL was not applied.");
        }
        command.CommandText = """
            CREATE TABLE DEV_TEMPLATE.HR_BRANCH_ATTENDANCE_LOCATION (
                BRANCH_ID NUMBER(19) PRIMARY KEY, COMPANY_ID NUMBER(19) NOT NULL,
                LATITUDE NUMBER(10,7) NULL, LONGITUDE NUMBER(11,7) NULL,
                RADIUS_METERS NUMBER(10,2) NOT NULL, ENFORCE_GEOFENCE NUMBER(1) NOT NULL,
                CREATION_USER NVARCHAR2(200) NOT NULL, CREATION_DATE TIMESTAMP(7) NOT NULL,
                UPDATE_USER NVARCHAR2(200) NULL, UPDATE_DATE TIMESTAMP(7) NULL,
                CONSTRAINT FK_HR_BRANCH_LOCATION FOREIGN KEY (BRANCH_ID) REFERENCES DEV_TEMPLATE.SYS_BRANCH("Id"),
                CONSTRAINT CK_HR_BRANCH_LOCATION CHECK (RADIUS_METERS>0 AND ENFORCE_GEOFENCE IN (0,1)
                  AND ((LATITUDE IS NULL AND LONGITUDE IS NULL AND ENFORCE_GEOFENCE=0)
                    OR (LATITUDE BETWEEN -90 AND 90 AND LONGITUDE BETWEEN -180 AND 180 AND LATITUDE IS NOT NULL AND LONGITUDE IS NOT NULL)))
            )
            """;
        await command.ExecuteNonQueryAsync(); report.Add("Created branch attendance location table with restrictive branch FK and GPS check. No branch configured; no ERP branch columns changed.");
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        await VerifyConstraints(connection);
    }

    private static async Task VerifyBranchParentKey(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='SYS_BRANCH' AND COLUMN_NAME='Id' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND NVL(DATA_SCALE,0)=0 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.SYS_BRANCH.\"Id\" is not a required NUMBER(19,0); branch-location DDL was not applied.");

        command.Parameters.Clear();
        command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS c WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='SYS_BRANCH' AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED' AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1 AND (SELECT MIN(cc.COLUMN_NAME) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='Id'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
            throw new InvalidOperationException("DEV_TEMPLATE.SYS_BRANCH lacks an enabled, validated unique key on quoted column Id; branch-location DDL was not applied.");
    }

    private static async Task VerifyConstraints(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS c WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_BRANCH_ATTENDANCE_LOCATION' AND c.CONSTRAINT_TYPE='P' AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED' AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1 AND (SELECT MIN(cc.COLUMN_NAME) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='BRANCH_ID'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("HR_BRANCH_ATTENDANCE_LOCATION requires an enabled, validated primary key on BRANCH_ID.");

        command.Parameters.Clear();
        command.CommandText = "SELECT COUNT(*) FROM ALL_CONSTRAINTS c WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME='HR_BRANCH_ATTENDANCE_LOCATION' AND c.CONSTRAINT_NAME='FK_HR_BRANCH_LOCATION' AND c.CONSTRAINT_TYPE='R' AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED' AND c.DELETE_RULE='NO ACTION' AND (SELECT LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='BRANCH_ID' AND c.R_OWNER='DEV_TEMPLATE' AND c.R_CONSTRAINT_NAME IN (SELECT p.CONSTRAINT_NAME FROM ALL_CONSTRAINTS p WHERE p.OWNER='DEV_TEMPLATE' AND p.TABLE_NAME='SYS_BRANCH' AND p.CONSTRAINT_TYPE IN ('P','U') AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED' AND (SELECT LISTAGG(pc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY pc.POSITION) FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME)='Id')";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("HR_BRANCH_ATTENDANCE_LOCATION.FK_HR_BRANCH_LOCATION does not match the validated SYS_BRANCH(Id) restrictive relationship.");

        command.Parameters.Clear();
        command.CommandText = "SELECT SEARCH_CONDITION_VC FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_BRANCH_ATTENDANCE_LOCATION' AND CONSTRAINT_NAME='CK_HR_BRANCH_LOCATION' AND CONSTRAINT_TYPE='C' AND STATUS='ENABLED' AND VALIDATED='VALIDATED'";
        var condition = Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
        const string expectedCondition = "RADIUS_METERS>0 AND ENFORCE_GEOFENCE IN (0,1) AND ((LATITUDE IS NULL AND LONGITUDE IS NULL AND ENFORCE_GEOFENCE=0) OR (LATITUDE BETWEEN -90 AND 90 AND LONGITUDE BETWEEN -180 AND 180 AND LATITUDE IS NOT NULL AND LONGITUDE IS NOT NULL))";
        if (Normalize(condition) != Normalize(expectedCondition))
            throw new InvalidOperationException("HR_BRANCH_ATTENDANCE_LOCATION.CK_HR_BRANCH_LOCATION is missing, disabled, unvalidated, or has an unexpected condition.");
    }

    private static string Normalize(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '(' && c != ')' && c != '"').Select(char.ToUpperInvariant).ToArray());
}
