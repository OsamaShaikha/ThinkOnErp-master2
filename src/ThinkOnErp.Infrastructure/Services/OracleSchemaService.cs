using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

public class OracleSchemaService : IOracleSchemaService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OracleSchemaService> _logger;
    private readonly string _masterConnectionString;
    private readonly string _pdbConnectionString;

    private static readonly HashSet<string> GlobalTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS_SUPER_ADMIN", "SYS_COMPANY", "SYS_SYSTEM", "SYS_SCREEN",
        "SYS_CURRENCY", "SYS_SETTINGS", "SYS_CODE",
        "SYS_AUDIT_LOG", "SYS_AUDIT_LOG_ARCHIVE", "SYS_RETENTION_POLICIES",
        "SYS_BRANCH",
        "SYS_DOCUMENT", "SYS_REQUEST_TICKET",
        "SYS_TICKET_ATTACHMENT", "SYS_TICKET_CATEGORY", "SYS_TICKET_COMMENT",
        "SYS_TICKET_CONFIG", "SYS_TICKET_PRIORITY", "SYS_TICKET_STATUS", "SYS_TICKET_TYPE",
        "SYS_SECURITY_THREATS", "SYS_FAILED_LOGINS",
        "SYS_PERFORMANCE_METRICS", "SYS_SLOW_QUERIES", "SYS_REPORT_SCHEDULE",
        "SYS_FEATURE", "SYS_SCREEN_FEATURE", "SYS_BRANCH_SYSTEMS",
        "SYS_BRANCH_SCREENS", "SYS_BRANCH_FEATURES",
        "__EFMigrationsHistory"
    };

    public OracleSchemaService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OracleSchemaService> logger,
        PasswordHashingService passwordHashingService)
    {
        _scopeFactory = scopeFactory;
        _masterConnectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Connection string 'OracleDb' not found.");
        _pdbConnectionString = _masterConnectionString.Replace(
            "SERVICE_NAME=free)", "SERVICE_NAME=FREEPDB1)", StringComparison.OrdinalIgnoreCase);
        _logger = logger;
        _passwordHashingService = passwordHashingService;
    }

    private readonly PasswordHashingService _passwordHashingService;

    public async Task CreateCompanySchemaAsync(string schemaName, string password)
    {
        schemaName = schemaName.ToUpperInvariant();
        _logger.LogInformation("Creating Oracle schema: {SchemaName}", schemaName);

        await using var masterConn = new OracleConnection(_masterConnectionString);
        await masterConn.OpenAsync();

        // Enable Oracle 12c+ script mode to allow regular usernames in CDB
        await ExecuteRawAsync(masterConn, "ALTER SESSION SET \"_ORACLE_SCRIPT\" = TRUE");

        // 1. Create the Oracle user (schema)
        try
        {
            await ExecuteRawAsync(masterConn, $"CREATE USER \"{schemaName}\" IDENTIFIED BY \"{password}\"");
            _logger.LogInformation("User created: {SchemaName}", schemaName);
        }
        catch (OracleException ex) when (ex.Number == 1920)
        {
            _logger.LogWarning("Schema already exists: {SchemaName}", schemaName);
        }

        // 2. Grant necessary privileges, tablespace quota, and ensure account is unlocked
        await ExecuteRawAsync(masterConn,
            $"GRANT CONNECT, RESOURCE, CREATE SESSION, CREATE TABLE, CREATE VIEW, CREATE SEQUENCE, CREATE PROCEDURE, CREATE TRIGGER, CREATE SYNONYM TO \"{schemaName}\"");
        await ExecuteRawAsync(masterConn,
            $"ALTER USER \"{schemaName}\" QUOTA UNLIMITED ON USERS");
        await ExecuteRawAsync(masterConn,
            $"ALTER USER \"{schemaName}\" ACCOUNT UNLOCK");
        _logger.LogInformation("Privileges and quota granted to: {SchemaName}", schemaName);

        // 3. Create tenant tables
        await CreateTenantTablesAsync(masterConn, schemaName);

        // 4. Grant privileges on tenant tables to master user for cross-schema access
        await GrantTenantTableAccessAsync(schemaName, password);

        // 5. Create synonyms for global tables in tenant schema so ALTER SESSION CURRENT_SCHEMA works
        await CreateGlobalSynonymsAsync(schemaName, password);

        _logger.LogInformation("Schema creation completed: {SchemaName}", schemaName);
    }

    public async Task SeedDefaultAdminAsync(string schemaName, string schemaPassword, string defaultPassword, long companyId, long branchId, string creationUser)
    {
        schemaName = schemaName.ToUpperInvariant();
        _logger.LogInformation("Seeding default admin user in schema: {SchemaName}", schemaName);

        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, schemaPassword);

        var hashedPassword = _passwordHashingService.HashPassword(defaultPassword);

        var roleId = 1L;
        try
        {
            var roleSql = $"INSERT INTO \"{schemaName}\".\"SYS_ROLE\" " +
                $"(\"Id\", \"NAME_AR\", \"NAME_EN\", \"NOTE\", \"IS_ACTIVE\", \"CREATION_USER\", \"CREATION_DATE\") " +
                $"VALUES (:roleId, 'مدير النظام', 'Administrator', 'Default system administrator', 1, :creationUser, SYSDATE)";
            await using var roleCmd = tenantConn.CreateCommand();
            roleCmd.CommandText = roleSql;
            roleCmd.Parameters.Add(new OracleParameter("roleId", roleId));
            roleCmd.Parameters.Add(new OracleParameter("creationUser", creationUser));
            await roleCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Default Administrator role created in {SchemaName}", schemaName);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            _logger.LogDebug("Default role already exists in {SchemaName}", schemaName);
        }

        try
        {
            var userSql = $"INSERT INTO \"{schemaName}\".\"SYS_USERS\" " +
                $"(\"Id\", \"NAME_AR\", \"NAME_EN\", \"USER_NAME\", \"PASSWORD\", \"ROLE\", \"BRANCH_ID\", \"COMPANY_ID\", " +
                $"\"IS_ACTIVE\", \"IS_ADMIN\", \"CREATION_USER\", \"CREATION_DATE\") " +
                $"VALUES (1, 'مدير النظام', 'Admin', 'admin', :password, :roleId, :branchId, :companyId, 1, 1, :creationUser, SYSDATE)";
            await using var userCmd = tenantConn.CreateCommand();
            userCmd.CommandText = userSql;
            userCmd.Parameters.Add(new OracleParameter("password", hashedPassword));
            userCmd.Parameters.Add(new OracleParameter("roleId", roleId));
            userCmd.Parameters.Add(new OracleParameter("branchId", branchId));
            userCmd.Parameters.Add(new OracleParameter("companyId", companyId));
            userCmd.Parameters.Add(new OracleParameter("creationUser", creationUser));
            await userCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Default admin user created in {SchemaName}", schemaName);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            _logger.LogDebug("Default admin user already exists in {SchemaName}", schemaName);
        }
    }

    private static string BuildTenantConnectionString(string baseConnectionString, string schemaName, string schemaPassword)
    {
        var csb = new OracleConnectionStringBuilder(baseConnectionString)
        {
            UserID = schemaName,
            Password = schemaPassword
        };
        return csb.ConnectionString;
    }

    /// <summary>
    /// Creates a tenant connection, trying PDB (FREEPDB1) first for manually-created schemas,
    /// then falling back to CDB$ROOT for schemas created via the API.
    /// </summary>
    private async Task<OracleConnection> OpenTenantConnectionAsync(string schemaName, string schemaPassword)
    {
        // Try PDB first
        var pdbCs = BuildTenantConnectionString(_pdbConnectionString, schemaName, schemaPassword);
        try
        {
            var conn = new OracleConnection(pdbCs);
            await conn.OpenAsync();
            return conn;
        }
        catch (OracleException ex) when (ex.Number == 1017)
        {
            _logger.LogDebug("PDB connection failed for {Schema}, falling back to CDB", schemaName);
        }

        // Fallback to CDB$ROOT
        var cdbCs = BuildTenantConnectionString(_masterConnectionString, schemaName, schemaPassword);
        var cdbConn = new OracleConnection(cdbCs);
        await cdbConn.OpenAsync();
        return cdbConn;
    }

    /// <summary>
    /// Opens a connection as the master user (THINKON_ERP), trying PDB first then CDB$ROOT.
    /// </summary>
    private async Task<OracleConnection> OpenMasterConnectionAsync()
    {
        // Try PDB first for environments where THINKON_ERP exists there
        try
        {
            var conn = new OracleConnection(_pdbConnectionString);
            await conn.OpenAsync();
            return conn;
        }
        catch (OracleException ex) when (ex.Number == 1017)
        {
            _logger.LogDebug("PDB master connection failed, falling back to CDB");
        }

        var cdbConn = new OracleConnection(_masterConnectionString);
        await cdbConn.OpenAsync();
        return cdbConn;
    }

    public async Task GrantUserPrivilegesAsync(string schemaName)
    {
        schemaName = schemaName.ToUpperInvariant();
        await using var conn = await OpenMasterConnectionAsync();
        await ExecuteRawAsync(conn, $"GRANT CONNECT, RESOURCE, CREATE SESSION, CREATE TABLE, CREATE VIEW, CREATE SEQUENCE, CREATE PROCEDURE, CREATE TRIGGER, CREATE SYNONYM TO \"{schemaName}\"");
        await ExecuteRawAsync(conn, $"ALTER USER \"{schemaName}\" QUOTA UNLIMITED ON USERS");
    }

    public async Task<bool> UnlockUserAccountAsync(string schemaName)
    {
        schemaName = schemaName.ToUpperInvariant();
        try
        {
            await using var conn = await OpenMasterConnectionAsync();
            await ExecuteRawAsync(conn, $"ALTER USER \"{schemaName}\" ACCOUNT UNLOCK");
            return true;
        }
        catch (OracleException ex) when (ex.Number == 1435 || ex.Number == 65048)
        {
            _logger.LogWarning("Cannot unlock user {Schema}: user does not exist", schemaName);
            return false;
        }
    }

    public async Task SaveRefreshTokenAsync(string schemaName, string schemaPassword, long userId, string refreshToken, DateTime expiryDate)
    {
        schemaName = schemaName.ToUpperInvariant();

        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, schemaPassword);

        var sql = $"UPDATE \"{schemaName}\".\"SYS_USERS\" SET " +
                  $"\"REFRESH_TOKEN\" = :refreshToken, " +
                  $"\"REFRESH_TOKEN_EXPIRY\" = :expiryDate, " +
                  $"\"UPDATE_DATE\" = SYSDATE " +
                  $"WHERE \"Id\" = :userId";

        await using var cmd = tenantConn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new OracleParameter("refreshToken", refreshToken));
        cmd.Parameters.Add(new OracleParameter("expiryDate", expiryDate));
        cmd.Parameters.Add(new OracleParameter("userId", userId));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<SysUser?> GetUserByUserNameAsync(string schemaName, string schemaPassword, string userName)
    {
        schemaName = schemaName.ToUpperInvariant();

        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, schemaPassword);

        var sql = $"SELECT \"Id\", \"NAME_AR\", \"NAME_EN\", \"USER_NAME\", \"PASSWORD\", \"ROLE\", \"BRANCH_ID\", \"COMPANY_ID\", " +
                  $"\"IS_ACTIVE\", \"IS_ADMIN\", \"CREATION_USER\", \"CREATION_DATE\", \"UPDATE_USER\", \"UPDATE_DATE\", " +
                  $"\"REFRESH_TOKEN\", \"REFRESH_TOKEN_EXPIRY\", \"FORCE_LOGOUT_DATE\" " +
                  $"FROM \"{schemaName}\".\"SYS_USERS\" WHERE \"USER_NAME\" = :userName AND \"IS_ACTIVE\" = 1";

        await using var cmd = tenantConn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new OracleParameter("userName", userName));

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SysUser
            {
                Id = reader.GetInt64(0),
                FullNameAr = reader.GetString(1),
                FullNameEn = reader.GetString(2),
                UserName = reader.GetString(3),
                Password = reader.GetString(4),
                RoleId = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                BranchId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                CompanyId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                IsActive = reader.GetInt64(8) == 1,
                IsAdmin = reader.GetInt64(9) == 1,
                CreationUser = reader.GetString(10),
                CreationDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                UpdateUser = reader.IsDBNull(12) ? null : reader.GetString(12),
                UpdateDate = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                RefreshToken = reader.IsDBNull(14) ? null : reader.GetString(14),
                RefreshTokenExpiry = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
                ForceLogoutDate = reader.IsDBNull(16) ? null : reader.GetDateTime(16)
            };
        }

        return null;
    }

    private async Task CreateTenantTablesAsync(OracleConnection connection, string schemaName)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
        var model = context.Model;

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName) || GlobalTables.Contains(tableName))
                continue;

            var createSql = GenerateCreateTableSql(entityType, schemaName, tableName);
            _logger.LogDebug("Creating table {SchemaName}.{TableName}", schemaName, tableName);
            try
            {
                await ExecuteRawAsync(connection, createSql);
            }
            catch (OracleException ex) when (ex.Number == 955)
            {
                _logger.LogDebug("Table {SchemaName}.{TableName} already exists, skipping", schemaName, tableName);
            }
        }
    }

    private static string GenerateCreateTableSql(IEntityType entityType, string schemaName, string tableName)
    {
        var columns = new List<string>();
        var primaryKeys = new List<string>();

        foreach (var prop in entityType.GetProperties())
        {
            var storeId = StoreObjectIdentifier.Table(tableName);
            var colName = prop.GetColumnName(storeId);
            var colType = prop.GetColumnType();

            if (string.IsNullOrEmpty(colType))
                colType = GetOracleType(prop);

            var nullable = prop.IsNullable ? "NULL" : "NOT NULL";
            var identity = IsIdentityColumn(prop) ? " GENERATED BY DEFAULT AS IDENTITY" : string.Empty;
            var colDef = $"    \"{colName}\" {colType}{identity} {nullable}";

            columns.Add(colDef);

            if (prop.IsPrimaryKey())
                primaryKeys.Add($"\"{colName}\"");
        }

        var sql = $"CREATE TABLE \"{schemaName}\".\"{tableName}\"\n(\n";
        sql += string.Join(",\n", columns);

        if (primaryKeys.Count > 0)
        {
            sql += $",\n    PRIMARY KEY ({string.Join(", ", primaryKeys)})";
        }

        sql += "\n)";
        return sql;
    }

    private static bool IsIdentityColumn(IProperty prop)
    {
        if (!prop.IsPrimaryKey() || prop.ValueGenerated != ValueGenerated.OnAdd)
            return false;

        var clrType = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
        return clrType == typeof(short)
            || clrType == typeof(int)
            || clrType == typeof(long);
    }

    private static string GetOracleType(IProperty prop)
    {
        var clrType = prop.ClrType;
        var maxLength = prop.GetMaxLength();

        if (clrType == typeof(bool)) return "NUMBER(1)";
        if (clrType == typeof(byte)) return "NUMBER(3)";
        if (clrType == typeof(short)) return "NUMBER(6)";
        if (clrType == typeof(int)) return "NUMBER(10)";
        if (clrType == typeof(long)) return "NUMBER(19)";
        if (clrType == typeof(float)) return "BINARY_FLOAT";
        if (clrType == typeof(double)) return "BINARY_DOUBLE";
        if (clrType == typeof(decimal)) return "NUMBER(18,2)";
        if (clrType == typeof(DateTime)) return "DATE";
        if (clrType == typeof(DateTimeOffset)) return "TIMESTAMP WITH TIME ZONE";
        if (clrType == typeof(TimeSpan)) return "INTERVAL DAY TO SECOND";
        if (clrType == typeof(Guid)) return "RAW(16)";
        if (clrType == typeof(string))
        {
            if (maxLength.HasValue && maxLength > 0 && maxLength <= 4000)
                return $"NVARCHAR2({maxLength})";
            if (maxLength > 4000)
                return "NCLOB";
            return "NVARCHAR2(2000)";
        }
        if (clrType == typeof(byte[]))
        {
            if (maxLength.HasValue && maxLength <= 2000)
                return $"RAW({maxLength})";
            return "BLOB";
        }

        return "NVARCHAR2(255)";
    }

    private static readonly HashSet<string> TenantOnlyTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS_ROLE", "SYS_USERS", "SYS_USERS_ROLES",
        "SYS_FISCAL_YEAR", "SYS_SAVED_SEARCH", "SYS_SEARCH_ANALYTICS",
        "SYS_ROLE_SCREEN_PERMISSIONS", "SYS_USER_SCREEN_PERMISSIONS"
    };

    private async Task GrantTenantTableAccessAsync(string schemaName, string password)
    {
        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, password);

        var masterUser = "THINKON_ERP";
        foreach (var table in TenantOnlyTables)
        {
            try
            {
                await ExecuteRawAsync(tenantConn,
                    $"GRANT SELECT, INSERT, UPDATE, DELETE ON \"{table}\" TO \"{masterUser}\"");
                _logger.LogDebug("Granted access on {Schema}.{Table} to {User}", schemaName, table, masterUser);
            }
            catch (OracleException ex) when (ex.Number == 942)
            {
                _logger.LogDebug("Table {Schema}.{Table} not found, skipping grant", schemaName, table);
            }
        }
    }

    private async Task CreateGlobalSynonymsAsync(string schemaName, string password)
    {
        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, password);

        // Create a synonym in the tenant schema for each global table
        foreach (var table in GlobalTables)
        {
            if (table == "__EFMigrationsHistory")
                continue;

            try
            {
                await ExecuteRawAsync(tenantConn,
                    $"CREATE SYNONYM \"{table}\" FOR \"THINKON_ERP\".\"{table}\"");
                _logger.LogDebug("Synonym created: {Schema}.{Table} -> THINKON_ERP.{Table}", schemaName, table);
            }
            catch (OracleException ex) when (ex.Number == 955)
            {
                _logger.LogDebug("Synonym already exists: {Schema}.{Table}", schemaName, table);
            }
        }
    }

    private static async Task ExecuteRawAsync(OracleConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpgradeExistingTenantSchemasAsync()
    {
        var masterConn = await OpenMasterConnectionAsync();
        await using var _ = masterConn.ConfigureAwait(false);

        // Get all company schemas
        var schemas = new List<string>();
        try
        {
            await using var cmd = masterConn.CreateCommand();
            cmd.CommandText = "SELECT COMPANY_SCHEMA FROM SYS_COMPANY WHERE COMPANY_SCHEMA IS NOT NULL";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                if (!string.IsNullOrEmpty(schema))
                    schemas.Add(schema);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query company schemas for upgrade");
            return;
        }

        if (schemas.Count == 0)
        {
            _logger.LogInformation("No existing tenant schemas to upgrade");
            return;
        }

        var oldTables = new[] { "SYS_ROLE_SCREEN_PERMISSIONS", "SYS_USER_SCREEN_PERMISSIONS" };

        foreach (var schema in schemas)
        {
            try
            {
                await using var pdbConn = await OpenTenantConnectionAsync(schema, schema);
                await using var __ = pdbConn.ConfigureAwait(false);

                foreach (var table in oldTables)
                {
                    try
                    {
                        await ExecuteRawAsync(pdbConn, $"DROP TABLE \"{schema}\".\"{table}\" CASCADE CONSTRAINTS PURGE");
                        _logger.LogInformation("Dropped old table {Schema}.{Table}", schema, table);
                    }
                    catch (OracleException ex) when (ex.Number == 942)
                    {
                        // 942 = table does not exist, that's fine
                    }
                }

                // Create new permission tables using EF model
                await CreateTenantTablesAsync(pdbConn, schema);
                _logger.LogInformation("Upgraded tenant schema: {Schema}", schema);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upgrade tenant schema: {Schema}", schema);
            }
        }
    }
}
