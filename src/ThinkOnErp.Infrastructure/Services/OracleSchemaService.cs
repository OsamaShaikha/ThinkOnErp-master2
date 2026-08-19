using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly PasswordHashingService _passwordHashingService;
    private readonly string _devSchemaName;
    private readonly string _devSchemaPassword;
    private readonly List<string> _objectTypesToClone;

    private static readonly HashSet<string> GlobalTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS_SUPER_ADMIN", "SYS_COMPANY", "SYS_SYSTEM", "SYS_SCREEN",
        "SYS_FEATURE", "SYS_SCREEN_FEATURE", "SYS_CURRENCY",
        "SYS_PERFORMANCE_METRICS", "SYS_SLOW_QUERIES", "SYS_SECURITY_THREATS", "SYS_FAILED_LOGINS",
        "SYS_API_CATEGORIES", "SYS_API_ENDPOINTS", "SYS_REPORT_SCHEDULE",
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

        _devSchemaName = configuration["TenantProvisioning:DeveloperSchemaName"] ?? "DEV_TEMPLATE";
        _devSchemaPassword = configuration["TenantProvisioning:DeveloperSchemaPassword"] ?? "DEV_TEMPLATE";
        _objectTypesToClone = configuration.GetSection("TenantProvisioning:ObjectTypesToClone").Get<List<string>>() 
            ?? new List<string> { "TABLE", "INDEX", "SEQUENCE", "TRIGGER", "CONSTRAINT" };
    }

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

        // 3. Clone tenant tables from developer schema
        await CloneFromDeveloperSchemaAsync(masterConn, schemaName);

        // Ensure accounting tables, constraints, indexes, and reference data exist even when
        // the developer template was created by an older application version.
        await EnsureAccountingSchemaAsync(masterConn, schemaName);

        // Reset identity sequences for tenant tables in the new schema to start from 1
        var tablesToResetTo1 = new[]
        {
            "SYS_BRANCH", "SYS_USERS", "SYS_USERS_ROLES", "SYS_USER_BRANCHES",
            "SYS_FISCAL_YEAR", "SYS_ROLE", "SYS_SAVED_SEARCH", "SYS_REQUEST_TICKET",
            "SYS_TICKET_COMMENT", "SYS_TICKET_ATTACHMENT", "GL_ACCOUNT"
        };
        foreach (var table in tablesToResetTo1)
        {
            try
            {
                await ExecuteRawAsync(masterConn, $"ALTER TABLE \"{schemaName}\".\"{table}\" MODIFY \"Id\" GENERATED BY DEFAULT AS IDENTITY (START WITH 1)");
                _logger.LogDebug("Reset identity sequence to START WITH 1 for {Schema}.{Table}", schemaName, table);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Sequence reset skipped for {Schema}.{Table}: {Msg}", schemaName, table, ex.Message);
            }
        }

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
                $"(\"Id\", \"NAME_AR\", \"NAME_EN\", \"USER_NAME\", \"PASSWORD\", \"ROLE\", \"COMPANY_ID\", " +
                $"\"IS_ACTIVE\", \"IS_ADMIN\", \"CREATION_USER\", \"CREATION_DATE\") " +
                $"VALUES (1, 'مدير النظام', 'Admin', 'admin', :password, :roleId, :companyId, 1, 1, :creationUser, SYSDATE)";
            await using var userCmd = tenantConn.CreateCommand();
            userCmd.CommandText = userSql;
            userCmd.Parameters.Add(new OracleParameter("password", hashedPassword));
            userCmd.Parameters.Add(new OracleParameter("roleId", roleId));
            userCmd.Parameters.Add(new OracleParameter("companyId", companyId));
            userCmd.Parameters.Add(new OracleParameter("creationUser", creationUser));
            await userCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Default admin user created in {SchemaName}", schemaName);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            _logger.LogDebug("Default admin user already exists in {SchemaName}", schemaName);
        }

        try
        {
            var userBranchSql = $"INSERT INTO \"{schemaName}\".\"SYS_USER_BRANCHES\" " +
                $"(\"USER_ID\", \"BRANCH_ID\", \"IS_PRIMARY\", \"ASSIGNED_BY\", \"ASSIGNED_AT\") " +
                $"VALUES (1, :branchId, 1, :creationUser, SYSDATE)";
            await using var ubCmd = tenantConn.CreateCommand();
            ubCmd.CommandText = userBranchSql;
            ubCmd.Parameters.Add(new OracleParameter("branchId", branchId));
            ubCmd.Parameters.Add(new OracleParameter("creationUser", creationUser));
            await ubCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Default admin user mapped to branch {BranchId} in {SchemaName}", branchId, schemaName);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            _logger.LogDebug("Default admin user branch mapping already exists in {SchemaName}", schemaName);
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

        var sql = $"SELECT u.\"Id\", u.\"NAME_AR\", u.\"NAME_EN\", u.\"USER_NAME\", u.\"PASSWORD\", u.\"ROLE\", ub.\"BRANCH_ID\", u.\"COMPANY_ID\", " +
                  $"u.\"IS_ACTIVE\", u.\"IS_ADMIN\", u.\"CREATION_USER\", u.\"CREATION_DATE\", u.\"UPDATE_USER\", u.\"UPDATE_DATE\", " +
                  $"u.\"REFRESH_TOKEN\", u.\"REFRESH_TOKEN_EXPIRY\", u.\"FORCE_LOGOUT_DATE\", u.\"PHONE\", u.\"PHONE2\", u.\"EMAIL\", u.\"LAST_LOGIN_DATE\" " +
                  $"FROM \"{schemaName}\".\"SYS_USERS\" u " +
                  $"LEFT JOIN \"{schemaName}\".\"SYS_USER_BRANCHES\" ub ON u.\"Id\" = ub.\"USER_ID\" AND ub.\"IS_PRIMARY\" = 1 " +
                  $"WHERE u.\"USER_NAME\" = :userName AND u.\"IS_ACTIVE\" = 1";

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
                IsActive = reader.IsDBNull(8) ? false : (reader.GetValue(8)?.ToString()?.Trim() is "1" or "Y" or "true" or "TRUE"),
                IsAdmin = reader.IsDBNull(9) ? false : (reader.GetValue(9)?.ToString()?.Trim() is "1" or "Y" or "true" or "TRUE"),
                CreationUser = reader.GetString(10),
                CreationDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                UpdateUser = reader.IsDBNull(12) ? null : reader.GetString(12),
                UpdateDate = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                RefreshToken = reader.IsDBNull(14) ? null : reader.GetString(14),
                RefreshTokenExpiry = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
                ForceLogoutDate = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                Phone = reader.IsDBNull(17) ? null : reader.GetString(17),
                Phone2 = reader.IsDBNull(18) ? null : reader.GetString(18),
                Email = reader.IsDBNull(19) ? null : reader.GetString(19),
                LastLoginDate = reader.IsDBNull(20) ? null : reader.GetDateTime(20)
            };
        }

        return null;
    }

    public async Task<List<long>> GetUserBranchIdsAsync(string schemaName, string schemaPassword, long userId)
    {
        schemaName = schemaName.ToUpperInvariant();

        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, schemaPassword);

        var sql = $"SELECT \"BRANCH_ID\" FROM \"{schemaName}\".\"SYS_USER_BRANCHES\" WHERE \"USER_ID\" = :userId";

        await using var cmd = tenantConn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new OracleParameter("userId", userId));

        var branchIds = new List<long>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                branchIds.Add(reader.GetInt64(0));
            }
        }

        return branchIds;
    }

    private async Task BootstrapTablesFromEfModelAsync(OracleConnection connection, string schemaName)
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

            if (prop.GetValueConverter() != null || string.IsNullOrEmpty(colType))
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
        var converter = prop.GetValueConverter();
        var clrType = converter != null ? converter.ProviderClrType : prop.ClrType;
        clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;
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
        "ACCOUNT_CATEGORY", "GL_ACCOUNT", "GL_ACCOUNT_BRANCH", "GL_ACCOUNT_STRUCTURE_CONFIG",
        "SYS_ROLE", "SYS_USERS", "SYS_USERS_ROLES", "SYS_USER_BRANCHES",
        "SYS_FISCAL_YEAR", "SYS_SAVED_SEARCH", "SYS_SEARCH_ANALYTICS",
        "SYS_ROLE_SCREEN_PERMISSIONS", "SYS_USER_SCREEN_PERMISSIONS",
        "SYS_BRANCH", "SYS_BRANCH_FEATURES", "SYS_BRANCH_SCREENS", "SYS_BRANCH_SYSTEMS",
        "SYS_AUDIT_LOG", "SYS_AUDIT_LOG_ARCHIVE", "SYS_RETENTION_POLICIES", "SYS_SETTINGS", "SYS_CODE",
        "SYS_DOCUMENT", "SYS_REQUEST_TICKET", "SYS_TICKET_ATTACHMENT", "SYS_TICKET_CATEGORY", "SYS_TICKET_COMMENT",
        "SYS_TICKET_CONFIG", "SYS_TICKET_PRIORITY", "SYS_TICKET_STATUS", "SYS_TICKET_TYPE"
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

    /// <summary>
    /// Ensures the tenant-local chart-of-accounts schema is complete. The generic EF bootstrap
    /// intentionally creates only columns and primary keys, so Oracle-specific defaults,
    /// constraints, and indexes are applied explicitly here. Every statement is safe to repeat.
    /// </summary>
    private async Task EnsureAccountingSchemaAsync(OracleConnection connection, string schemaName)
    {
        schemaName = NormalizeOracleIdentifier(schemaName);
        _logger.LogInformation("Ensuring accounting schema objects in {SchemaName}", schemaName);

        try
        {
            var migrateSql = $@"
                DECLARE
                    v_col NUMBER;
                    v_type VARCHAR2(100);
                BEGIN
                    SELECT COUNT(*) INTO v_col FROM all_tables WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT';
                    IF v_col > 0 THEN
                        -- 1. Drop old PK on Id if not on ACCOUNT_CODE
                        FOR pk IN (
                            SELECT constraint_name 
                            FROM all_constraints 
                            WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND constraint_type = 'P'
                        ) LOOP
                            SELECT COUNT(*) INTO v_col 
                            FROM all_cons_columns 
                            WHERE owner = '{schemaName}' AND constraint_name = pk.constraint_name AND column_name = 'ACCOUNT_CODE';
                            
                            IF v_col = 0 THEN
                                BEGIN
                                    EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" DROP CONSTRAINT ""' || pk.constraint_name || '"" CASCADE';
                                EXCEPTION WHEN OTHERS THEN NULL;
                                END;
                            END IF;
                        END LOOP;

                        -- 2. Ensure PK on ACCOUNT_CODE
                        SELECT COUNT(*) INTO v_col 
                        FROM all_constraints c
                        JOIN all_cons_columns cc ON c.owner = cc.owner AND c.constraint_name = cc.constraint_name
                        WHERE c.owner = '{schemaName}' AND c.table_name = 'GL_ACCOUNT' AND c.constraint_type = 'P' AND cc.column_name = 'ACCOUNT_CODE';

                        IF v_col = 0 THEN
                            BEGIN
                                EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" ADD CONSTRAINT ""PK_GL_ACC"" PRIMARY KEY (""ACCOUNT_CODE"")';
                            EXCEPTION WHEN OTHERS THEN NULL;
                            END;
                        END IF;

                        -- 3. Drop legacy PARENT_ACCOUNT_ID if present
                        SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND column_name = 'PARENT_ACCOUNT_ID';
                        IF v_col > 0 THEN
                            BEGIN
                                EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" DROP COLUMN ""PARENT_ACCOUNT_ID"" CASCADE CONSTRAINTS';
                            EXCEPTION WHEN OTHERS THEN NULL;
                            END;
                        END IF;

                        -- 4. Check PARENT_ACCOUNT_CODE column type
                        SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND column_name = 'PARENT_ACCOUNT_CODE';
                        IF v_col > 0 THEN
                            SELECT data_type INTO v_type FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND column_name = 'PARENT_ACCOUNT_CODE';
                            IF v_type NOT IN ('NVARCHAR2', 'VARCHAR2') THEN
                                EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" DROP COLUMN ""PARENT_ACCOUNT_CODE"" CASCADE CONSTRAINTS';
                                EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" ADD ""PARENT_ACCOUNT_CODE"" NVARCHAR2(50)';
                            END IF;
                        ELSE
                            EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" ADD ""PARENT_ACCOUNT_CODE"" NVARCHAR2(50)';
                        END IF;

                        -- 5. Check OLD_ACCOUNT_CODE column
                        SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND column_name = 'OLD_ACCOUNT_CODE';
                        IF v_col = 0 THEN
                            EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" ADD ""OLD_ACCOUNT_CODE"" NVARCHAR2(50)';
                        END IF;

                        -- 6. Drop obsolete Id column if present
                        SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND column_name = 'Id';
                        IF v_col > 0 THEN
                            BEGIN
                                EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" DROP COLUMN ""Id"" CASCADE CONSTRAINTS';
                            EXCEPTION WHEN OTHERS THEN NULL;
                            END;
                        END IF;

                        -- 7. Drop obsolete CATEGORY_ID column if present
                        SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND column_name = 'CATEGORY_ID';
                        IF v_col > 0 THEN
                            BEGIN
                                EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" DROP COLUMN ""CATEGORY_ID"" CASCADE CONSTRAINTS';
                            EXCEPTION WHEN OTHERS THEN NULL;
                            END;
                        END IF;

                        -- 8. Drop obsolete COMPANY_ID column if present
                        SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT' AND column_name = 'COMPANY_ID';
                        IF v_col > 0 THEN
                            BEGIN
                                EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""GL_ACCOUNT"" DROP COLUMN ""COMPANY_ID"" CASCADE CONSTRAINTS';
                            EXCEPTION WHEN OTHERS THEN NULL;
                            END;
                        END IF;
                    END IF;

                    -- 9. Upgrade GL_ACCOUNT_BRANCH if legacy GL_ACCOUNT_ID column exists
                    SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE owner = '{schemaName}' AND table_name = 'GL_ACCOUNT_BRANCH' AND column_name = 'GL_ACCOUNT_ID';
                    IF v_col > 0 THEN
                        EXECUTE IMMEDIATE 'DROP TABLE ""{schemaName}"".""GL_ACCOUNT_BRANCH"" CASCADE CONSTRAINTS';
                    END IF;
                END;";

            await ExecuteRawAsync(connection, migrateSql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Legacy accounting column migration warning for schema {SchemaName}", schemaName);
        }

        var tableStatements = new (string Name, string Sql)[]
        {
            (
                "ACCOUNT_CATEGORY",
                $"""
                CREATE TABLE "{schemaName}"."ACCOUNT_CATEGORY"
                (
                    "Id" NUMBER(19) NOT NULL,
                    "CATEGORY_CODE" NUMBER(2) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "NORMAL_BALANCE" NVARCHAR2(1) NOT NULL,
                    "FINANCIAL_STATEMENT" NVARCHAR2(20) NOT NULL,
                    "DISPLAY_ORDER" NUMBER(10) NOT NULL,
                    CONSTRAINT "PK_ACC_CAT" PRIMARY KEY ("Id")
                )
                """
            ),
            (
                "GL_ACCOUNT",
                $"""
                CREATE TABLE "{schemaName}"."GL_ACCOUNT"
                (
                    "ACCOUNT_CODE" NVARCHAR2(50) NOT NULL,
                    "ACCOUNT_NAME_AR" NVARCHAR2(200) NOT NULL,
                    "ACCOUNT_NAME_EN" NVARCHAR2(200) NOT NULL,
                    "PARENT_ACCOUNT_CODE" NVARCHAR2(50) NULL,
                    "ACCOUNT_LEVEL" NUMBER(2) NOT NULL,
                    "ACCOUNT_TYPE" NVARCHAR2(10) NOT NULL,
                    "NORMAL_BALANCE" NVARCHAR2(1) NOT NULL,
                    "IS_CONTRA" NUMBER(1) DEFAULT 0 NOT NULL,
                    "IS_CONTROL_ACCOUNT" NUMBER(1) DEFAULT 0 NOT NULL,
                    "CONTROL_ACCOUNT_TYPE" NVARCHAR2(20) NULL,
                    "IS_BRANCH_SPECIFIC" NUMBER(1) DEFAULT 0 NOT NULL,
                    "IS_CLEARING" NUMBER(1) DEFAULT 0 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "DESCRIPTION" NVARCHAR2(1000) NULL,
                    "NOTES" NVARCHAR2(2000) NULL,
                    "OLD_ACCOUNT_CODE" NVARCHAR2(50) NULL,
                    CONSTRAINT "PK_GL_ACC" PRIMARY KEY ("ACCOUNT_CODE")
                )
                """
            ),
            (
                "GL_ACCOUNT_BRANCH",
                $"""
                CREATE TABLE "{schemaName}"."GL_ACCOUNT_BRANCH"
                (
                    "ACCOUNT_CODE" NVARCHAR2(50) NOT NULL,
                    "BRANCH_ID" NUMBER(19) NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    CONSTRAINT "PK_GL_ACC_BRANCH" PRIMARY KEY ("ACCOUNT_CODE", "BRANCH_ID")
                )
                """
            ),
            (
                "GL_COST_CENTER",
                $"""
                CREATE TABLE "{schemaName}"."GL_COST_CENTER"
                (
                    "COST_CENTER_CODE" NVARCHAR2(50) NOT NULL,
                    "PARENT_COST_CENTER_CODE" NVARCHAR2(50) NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "COST_CENTER_LEVEL" NUMBER(2) DEFAULT 1 NOT NULL,
                    "COST_CENTER_TYPE" NVARCHAR2(20) DEFAULT 'DETAIL' NOT NULL,
                    "IS_POSTABLE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_COST_CENTER" PRIMARY KEY ("COST_CENTER_CODE")
                )
                """
            ),
            (
                "GL_VOUCHER_TYPE",
                $"""
                CREATE TABLE "{schemaName}"."GL_VOUCHER_TYPE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "TYPE_CODE" NUMBER(6) NOT NULL,
                    "TYPE_KEY" NVARCHAR2(20) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "PREFIX" NVARCHAR2(10) NOT NULL,
                    "CATEGORY" NVARCHAR2(20) NOT NULL,
                    "SERIAL_RESET_POLICY" NVARCHAR2(20) DEFAULT 'MONTHLY' NOT NULL,
                    "REQUIRES_REVIEW" NUMBER(1) DEFAULT 1 NOT NULL,
                    "ALLOW_MANUAL_ENTRY" NUMBER(1) DEFAULT 1 NOT NULL,
                    "IS_SYSTEM" NUMBER(1) DEFAULT 0 NOT NULL,
                    "DISPLAY_ORDER" NUMBER(5) DEFAULT 1 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "DESCRIPTION" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_VOUCHER_TYPE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "GL_VOUCHER_HEADER",
                $"""
                CREATE TABLE "{schemaName}"."GL_VOUCHER_HEADER"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "BRANCH_ID" NUMBER(19) NOT NULL,
                    "FISCAL_YEAR_ID" NUMBER(19) NOT NULL,
                    "VOUCHER_YEAR" NUMBER(4) NOT NULL,
                    "VOUCHER_MONTH" NUMBER(2) NOT NULL,
                    "VOUCHER_TYPE" NUMBER(6) NOT NULL,
                    "VOUCHER_NO" NUMBER(19) NOT NULL,
                    "VOUCHER_DATE" DATE NOT NULL,
                    "DESCRIPTION" NVARCHAR2(2000) NULL,
                    "TOTAL_AMOUNT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_LOCAL_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_LOCAL_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "STATUS" NUMBER(2) DEFAULT 1 NOT NULL,
                    "IS_AUTO_RECORD" NUMBER(1) DEFAULT 0 NOT NULL,
                    "SOURCE_SYSTEM_CODE" NVARCHAR2(100) NULL,
                    "SOURCE_REF_ID" NUMBER(19) NULL,
                    "IS_STANDBY" NUMBER(1) DEFAULT 0 NOT NULL,
                    "IS_REVIEWED" NUMBER(1) DEFAULT 0 NOT NULL,
                    "REVIEW_USER" NVARCHAR2(100) NULL,
                    "REVIEW_DATE" DATE NULL,
                    "POST_USER" NVARCHAR2(100) NULL,
                    "POST_DATE" DATE NULL,
                    "UNPOST_USER" NVARCHAR2(100) NULL,
                    "UNPOST_DATE" DATE NULL,
                    "IS_REVERSED" NUMBER(1) DEFAULT 0 NOT NULL,
                    "REVERSE_USER" NVARCHAR2(100) NULL,
                    "REVERSE_DATE" DATE NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_VOUCHER_HEADER" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "GL_VOUCHER_DETAIL",
                $"""
                CREATE TABLE "{schemaName}"."GL_VOUCHER_DETAIL"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "VOUCHER_ID" NUMBER(19) NOT NULL,
                    "LINE_SER" NUMBER(6) NOT NULL,
                    "ACCOUNT_CODE" NVARCHAR2(50) NOT NULL,
                    "DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "BASE_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "BASE_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "DESCRIPTION" NVARCHAR2(1000) NULL,
                    "CURRENCY_ID" NUMBER(19) DEFAULT 1 NOT NULL,
                    "EXCHANGE_RATE" NUMBER(14,6) DEFAULT 1 NOT NULL,
                    "COST_CENTER_CODE" NVARCHAR2(50) NULL,
                    "COST_CENTER_MGR_CODE" NVARCHAR2(50) NULL,
                    "COST_CENTER_MNR_CODE" NVARCHAR2(50) NULL,
                    "IS_SETTLEMENT" NUMBER(1) DEFAULT 0 NOT NULL,
                    "PARTY_TYPE" NVARCHAR2(20) NULL,
                    "PARTY_CODE" NVARCHAR2(50) NULL,
                    "BRANCH_ID" NUMBER(19) NULL,
                    CONSTRAINT "PK_GL_VOUCHER_DETAIL" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "GL_VOUCHER_SERIAL",
                $"""
                CREATE TABLE "{schemaName}"."GL_VOUCHER_SERIAL"
                (
                    "BRANCH_ID" NUMBER(19) NOT NULL,
                    "SERIAL_YEAR" NUMBER(4) NOT NULL,
                    "SERIAL_MONTH" NUMBER(2) NOT NULL,
                    "VOUCHER_TYPE" NUMBER(6) NOT NULL,
                    "LAST_SERIAL_NO" NUMBER(19) DEFAULT 0 NOT NULL,
                    CONSTRAINT "PK_GL_VOUCHER_SERIAL" PRIMARY KEY ("BRANCH_ID", "SERIAL_YEAR", "SERIAL_MONTH", "VOUCHER_TYPE")
                )
                """
            ),
            (
                "GL_FISCAL_PERIOD",
                $"""
                CREATE TABLE "{schemaName}"."GL_FISCAL_PERIOD"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "FISCAL_YEAR_ID" NUMBER(19) NOT NULL,
                    "PERIOD_NUMBER" NUMBER(3) NOT NULL,
                    "PERIOD_NAME_AR" NVARCHAR2(200) NOT NULL,
                    "PERIOD_NAME_EN" NVARCHAR2(200) NOT NULL,
                    "START_DATE" DATE NOT NULL,
                    "END_DATE" DATE NOT NULL,
                    "STATUS" NVARCHAR2(20) DEFAULT 'OPEN' NOT NULL,
                    "IS_ADJUSTMENT" NUMBER(1) DEFAULT 0 NOT NULL,
                    "CLOSE_REASON" NVARCHAR2(500) NULL,
                    "CLOSED_BY" NVARCHAR2(100) NULL,
                    "CLOSED_DATE" DATE NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_FISCAL_PERIOD" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "GL_ACCOUNT_BALANCE",
                $"""
                CREATE TABLE "{schemaName}"."GL_ACCOUNT_BALANCE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "ACCOUNT_CODE" NVARCHAR2(50) NOT NULL,
                    "BRANCH_ID" NUMBER(19) NOT NULL,
                    "FISCAL_YEAR_ID" NUMBER(19) NOT NULL,
                    "FISCAL_PERIOD_ID" NUMBER(19) NOT NULL,
                    "CURRENCY_ID" NUMBER(19) NOT NULL,
                    "OPENING_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "OPENING_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "PERIOD_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "PERIOD_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CLOSING_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CLOSING_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_OPENING_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_OPENING_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_PERIOD_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_PERIOD_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_CLOSING_DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_CLOSING_CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_ACCOUNT_BALANCE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "CUSTOMER",
                $"""
                CREATE TABLE "{schemaName}"."CUSTOMER"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "CUSTOMER_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "AR_CONTROL_ACCOUNT_CODE" NVARCHAR2(50) DEFAULT '112101' NOT NULL,
                    "DEFAULT_CURRENCY_ID" NUMBER(19) NULL,
                    "CREDIT_LIMIT" NUMBER(18,3) NULL,
                    "PAYMENT_TERMS_DAYS" NUMBER(5) DEFAULT 30 NOT NULL,
                    "BRANCH_ID" NUMBER(19) NULL,
                    "TAX_NUMBER" NVARCHAR2(50) NULL,
                    "PHONE" NVARCHAR2(50) NULL,
                    "EMAIL" NVARCHAR2(100) NULL,
                    "ADDRESS" NVARCHAR2(500) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_CUSTOMER" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "VENDOR",
                $"""
                CREATE TABLE "{schemaName}"."VENDOR"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "VENDOR_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "AP_CONTROL_ACCOUNT_CODE" NVARCHAR2(50) DEFAULT '211101' NOT NULL,
                    "DEFAULT_CURRENCY_ID" NUMBER(19) NULL,
                    "PAYMENT_TERMS_DAYS" NUMBER(5) DEFAULT 30 NOT NULL,
                    "BRANCH_ID" NUMBER(19) NULL,
                    "TAX_NUMBER" NVARCHAR2(50) NULL,
                    "PHONE" NVARCHAR2(50) NULL,
                    "EMAIL" NVARCHAR2(100) NULL,
                    "ADDRESS" NVARCHAR2(500) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_VENDOR" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "AR_SUBLEDGER_TRANSACTION",
                $"""
                CREATE TABLE "{schemaName}"."AR_SUBLEDGER_TRANSACTION"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "CUSTOMER_CODE" NVARCHAR2(50) NOT NULL,
                    "JOURNAL_LINE_ID" NUMBER(19) NOT NULL,
                    "VOUCHER_ID" NUMBER(19) NOT NULL,
                    "TRANSACTION_TYPE" NVARCHAR2(30) DEFAULT 'INVOICE' NOT NULL,
                    "TRANSACTION_DATE" TIMESTAMP NOT NULL,
                    "DUE_DATE" TIMESTAMP NULL,
                    "AMOUNT" NUMBER(18,3) NOT NULL,
                    "CURRENCY_ID" NUMBER(19) NOT NULL,
                    "EXCHANGE_RATE" NUMBER(18,6) DEFAULT 1.0 NOT NULL,
                    "LOCAL_AMOUNT" NUMBER(18,3) NOT NULL,
                    "OPEN_AMOUNT" NUMBER(18,3) NOT NULL,
                    "LOCAL_OPEN_AMOUNT" NUMBER(18,3) NOT NULL,
                    "REFERENCE_NO" NVARCHAR2(100) NULL,
                    "DESCRIPTION" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_AR_SUB_TX" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "AR_CASH_APPLICATION",
                $"""
                CREATE TABLE "{schemaName}"."AR_CASH_APPLICATION"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "PAYMENT_TRANSACTION_ID" NUMBER(19) NOT NULL,
                    "INVOICE_TRANSACTION_ID" NUMBER(19) NOT NULL,
                    "APPLIED_AMOUNT" NUMBER(18,3) NOT NULL,
                    "LOCAL_APPLIED_AMOUNT" NUMBER(18,3) NOT NULL,
                    "APPLIED_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "NOTES" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_AR_CASH_APP" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "AP_SUBLEDGER_TRANSACTION",
                $"""
                CREATE TABLE "{schemaName}"."AP_SUBLEDGER_TRANSACTION"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "VENDOR_CODE" NVARCHAR2(50) NOT NULL,
                    "JOURNAL_LINE_ID" NUMBER(19) NOT NULL,
                    "VOUCHER_ID" NUMBER(19) NOT NULL,
                    "TRANSACTION_TYPE" NVARCHAR2(30) DEFAULT 'BILL' NOT NULL,
                    "TRANSACTION_DATE" TIMESTAMP NOT NULL,
                    "DUE_DATE" TIMESTAMP NULL,
                    "AMOUNT" NUMBER(18,3) NOT NULL,
                    "CURRENCY_ID" NUMBER(19) NOT NULL,
                    "EXCHANGE_RATE" NUMBER(18,6) DEFAULT 1.0 NOT NULL,
                    "LOCAL_AMOUNT" NUMBER(18,3) NOT NULL,
                    "OPEN_AMOUNT" NUMBER(18,3) NOT NULL,
                    "LOCAL_OPEN_AMOUNT" NUMBER(18,3) NOT NULL,
                    "REFERENCE_NO" NVARCHAR2(100) NULL,
                    "DESCRIPTION" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_AP_SUB_TX" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "AP_CASH_APPLICATION",
                $"""
                CREATE TABLE "{schemaName}"."AP_CASH_APPLICATION"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "PAYMENT_TRANSACTION_ID" NUMBER(19) NOT NULL,
                    "INVOICE_TRANSACTION_ID" NUMBER(19) NOT NULL,
                    "APPLIED_AMOUNT" NUMBER(18,3) NOT NULL,
                    "LOCAL_APPLIED_AMOUNT" NUMBER(18,3) NOT NULL,
                    "APPLIED_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "NOTES" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_AP_CASH_APP" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "GL_PDC_REGISTER",
                $"""
                CREATE TABLE "{schemaName}"."GL_PDC_REGISTER"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "BRANCH_ID" NUMBER(19) NOT NULL,
                    "FISCAL_YEAR_ID" NUMBER(19) NOT NULL,
                    "CHEQUE_TYPE" NVARCHAR2(20) DEFAULT 'RECEIVED' NOT NULL,
                    "CHEQUE_NO" NVARCHAR2(50) NOT NULL,
                    "CHEQUE_DATE" TIMESTAMP NOT NULL,
                    "DUE_DATE" TIMESTAMP NOT NULL,
                    "AMOUNT" NUMBER(18,3) NOT NULL,
                    "LOCAL_AMOUNT" NUMBER(18,3) NOT NULL,
                    "CURRENCY_ID" NUMBER(19) DEFAULT 1 NOT NULL,
                    "EXCHANGE_RATE" NUMBER(18,6) DEFAULT 1.0 NOT NULL,
                    "DRAWER_BANK_NAME" NVARCHAR2(150) NOT NULL,
                    "DRAWER_BANK_ACC_NO" NVARCHAR2(50) NULL,
                    "BENEFICIARY_NAME" NVARCHAR2(150) NULL,
                    "PARTY_TYPE" NVARCHAR2(20) NULL,
                    "PARTY_CODE" NVARCHAR2(50) NULL,
                    "STATUS" NVARCHAR2(20) DEFAULT 'RECEIVED' NOT NULL,
                    "INTERMEDIATE_ACCOUNT_CODE" NVARCHAR2(50) NULL,
                    "DEPOSIT_BANK_ACCOUNT_CODE" NVARCHAR2(50) NULL,
                    "DEPOSIT_DATE" TIMESTAMP NULL,
                    "CLEARED_DATE" TIMESTAMP NULL,
                    "BOUNCED_DATE" TIMESTAMP NULL,
                    "BOUNCE_REASON" NVARCHAR2(250) NULL,
                    "ORIGINATING_VOUCHER_ID" NUMBER(19) NULL,
                    "CLEARING_VOUCHER_ID" NUMBER(19) NULL,
                    "BOUNCE_VOUCHER_ID" NUMBER(19) NULL,
                    "NOTES" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_PDC_REGISTER" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "GL_POSTING_RULE",
                $"""
                CREATE TABLE "{schemaName}"."GL_POSTING_RULE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "BRANCH_ID" NUMBER(19) NULL,
                    "MODULE" NVARCHAR2(50) NOT NULL,
                    "EVENT_TYPE" NVARCHAR2(50) NOT NULL,
                    "EVENT_NAME_AR" NVARCHAR2(200) NOT NULL,
                    "EVENT_NAME_EN" NVARCHAR2(200) NOT NULL,
                    "DEBIT_ACCOUNT_CODE" NVARCHAR2(50) NOT NULL,
                    "CREDIT_ACCOUNT_CODE" NVARCHAR2(50) NOT NULL,
                    "DEFAULT_COST_CENTER_CODE" NVARCHAR2(50) NULL,
                    "DEFAULT_VOUCHER_TYPE" NUMBER(6) DEFAULT 1 NOT NULL,
                    "DESCRIPTION_TEMPLATE" NVARCHAR2(500) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_POSTING_RULE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "BANK_ACCOUNT",
                $"""
                CREATE TABLE "{schemaName}"."BANK_ACCOUNT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "BRANCH_ID" NUMBER(19) NOT NULL,
                    "ACCOUNT_NUMBER" NVARCHAR2(50) NOT NULL,
                    "ACCOUNT_NAME_AR" NVARCHAR2(200) NOT NULL,
                    "ACCOUNT_NAME_EN" NVARCHAR2(200) NOT NULL,
                    "BANK_NAME" NVARCHAR2(150) NOT NULL,
                    "BANK_BRANCH_NAME" NVARCHAR2(150) NULL,
                    "IBAN" NVARCHAR2(50) NULL,
                    "SWIFT_CODE" NVARCHAR2(30) NULL,
                    "CURRENCY_ID" NUMBER(19) DEFAULT 1 NOT NULL,
                    "GL_ACCOUNT_CODE" NVARCHAR2(50) DEFAULT '111201' NOT NULL,
                    "OVERDRAFT_LIMIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "OPENING_BALANCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CURRENT_BALANCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_BANK_ACCOUNT" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "CASH_REGISTER",
                $"""
                CREATE TABLE "{schemaName}"."CASH_REGISTER"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "BRANCH_ID" NUMBER(19) NOT NULL,
                    "CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "REGISTER_TYPE" NVARCHAR2(30) DEFAULT 'MAIN' NOT NULL,
                    "CUSTODIAN_NAME" NVARCHAR2(150) NULL,
                    "GL_ACCOUNT_CODE" NVARCHAR2(50) DEFAULT '111101' NOT NULL,
                    "CURRENCY_ID" NUMBER(19) DEFAULT 1 NOT NULL,
                    "MIN_LIMIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "MAX_LIMIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "OPENING_BALANCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CURRENT_BALANCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_CASH_REGISTER" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "BANK_RECONCILIATION",
                $"""
                CREATE TABLE "{schemaName}"."BANK_RECONCILIATION"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "BANK_ACCOUNT_ID" NUMBER(19) NOT NULL,
                    "FISCAL_YEAR_ID" NUMBER(19) NOT NULL,
                    "FISCAL_PERIOD_ID" NUMBER(19) NOT NULL,
                    "STATEMENT_DATE" TIMESTAMP NOT NULL,
                    "STATEMENT_ENDING_BALANCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "BOOK_ENDING_BALANCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_RECONCILED_AMOUNT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "UNRECONCILED_DIFFERENCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "STATUS" NVARCHAR2(30) DEFAULT 'DRAFT' NOT NULL,
                    "NOTES" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_BANK_RECONCILIATION" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "BANK_STATEMENT_LINE",
                $"""
                CREATE TABLE "{schemaName}"."BANK_STATEMENT_LINE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "RECONCILIATION_ID" NUMBER(19) NOT NULL,
                    "TRANSACTION_DATE" TIMESTAMP NOT NULL,
                    "VALUE_DATE" TIMESTAMP NULL,
                    "REFERENCE_NO" NVARCHAR2(100) NULL,
                    "CHEQUE_NO" NVARCHAR2(50) NULL,
                    "DESCRIPTION" NVARCHAR2(500) NOT NULL,
                    "DEBIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CREDIT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "BALANCE" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "IS_RECONCILED" NUMBER(1) DEFAULT 0 NOT NULL,
                    "RECONCILED_DATE" TIMESTAMP NULL,
                    "MATCHED_VOUCHER_DETAIL_ID" NUMBER(19) NULL,
                    CONSTRAINT "PK_BANK_STATEMENT_LINE" PRIMARY KEY ("ID")
                )
                """
            )
        };

        foreach (var statement in tableStatements)
        {
            await ExecuteAccountingDdlAsync(connection, $"table {statement.Name}", statement.Sql);
        }

        // BootstrapTablesFromEfModelAsync does not emit default clauses. MODIFY is idempotent
        // and also covers developer templates that were bootstrapped before these explicit DDLs.
        var defaultStatements = new[]
        {
            $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" MODIFY (\"IS_CONTRA\" DEFAULT 0)",
            $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" MODIFY (\"IS_CONTROL_ACCOUNT\" DEFAULT 0)",
            $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" MODIFY (\"IS_BRANCH_SPECIFIC\" DEFAULT 0)",
            $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" MODIFY (\"IS_CLEARING\" DEFAULT 0)",
            $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" MODIFY (\"IS_ACTIVE\" DEFAULT 1)",
            $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BRANCH\" MODIFY (\"IS_ACTIVE\" DEFAULT 1)"
        };

        foreach (var statement in defaultStatements)
        {
            await ExecuteRawAsync(connection, statement);
        }

        // Seed first so adding the category foreign key also succeeds for partially provisioned
        // schemas whose GL accounts exist but whose fixed category rows do not yet exist.
        await SeedAccountCategoriesAsync(connection, schemaName);
        await SeedVoucherTypesAsync(connection, schemaName);
        await SeedCostCentersAsync(connection, schemaName);

        var constraintStatements = new (string Name, string Sql)[]
        {
            ("PK_ACC_CAT", $"ALTER TABLE \"{schemaName}\".\"ACCOUNT_CATEGORY\" ADD CONSTRAINT \"PK_ACC_CAT\" PRIMARY KEY (\"Id\")"),
            ("UX_ACC_CATEGORY_CODE", $"ALTER TABLE \"{schemaName}\".\"ACCOUNT_CATEGORY\" ADD CONSTRAINT \"UX_ACC_CATEGORY_CODE\" UNIQUE (\"CATEGORY_CODE\")"),
            ("CK_ACC_CATEGORY_CODE", $"ALTER TABLE \"{schemaName}\".\"ACCOUNT_CATEGORY\" ADD CONSTRAINT \"CK_ACC_CATEGORY_CODE\" CHECK (\"CATEGORY_CODE\" BETWEEN 1 AND 8)"),
            ("CK_ACC_CATEGORY_BALANCE", $"ALTER TABLE \"{schemaName}\".\"ACCOUNT_CATEGORY\" CHECK (\"NORMAL_BALANCE\" IN ('D', 'C'))"),
            ("CK_ACC_CATEGORY_STATEMENT", $"ALTER TABLE \"{schemaName}\".\"ACCOUNT_CATEGORY\" ADD CONSTRAINT \"CK_ACC_CATEGORY_STATEMENT\" CHECK (\"FINANCIAL_STATEMENT\" IN ('BALANCE_SHEET', 'INCOME_STATEMENT'))"),

            ("PK_GL_ACC", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"PK_GL_ACC\" PRIMARY KEY (\"ACCOUNT_CODE\")"),
            ("FK_GL_ACC_PARENT", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"FK_GL_ACC_PARENT\" FOREIGN KEY (\"PARENT_ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),
            ("CK_GL_ACCOUNT_LEVEL", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"CK_GL_ACCOUNT_LEVEL\" CHECK (\"ACCOUNT_LEVEL\" BETWEEN 1 AND 10)"),
            ("CK_GL_ACCOUNT_TYPE", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"CK_GL_ACCOUNT_TYPE\" CHECK (\"ACCOUNT_TYPE\" IN ('HEADER', 'DETAIL'))"),
            ("CK_GL_ACCOUNT_BALANCE", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"CK_GL_ACCOUNT_BALANCE\" CHECK (\"NORMAL_BALANCE\" IN ('D', 'C'))"),
            ("CK_GL_ACCOUNT_FLAGS", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"CK_GL_ACCOUNT_FLAGS\" CHECK (\"IS_CONTRA\" IN (0, 1) AND \"IS_CONTROL_ACCOUNT\" IN (0, 1) AND \"IS_BRANCH_SPECIFIC\" IN (0, 1) AND \"IS_CLEARING\" IN (0, 1) AND \"IS_ACTIVE\" IN (0, 1))"),
            ("CK_GL_ACCOUNT_CONTROL_TYPE", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"CK_GL_ACCOUNT_CONTROL_TYPE\" CHECK ((\"IS_CONTROL_ACCOUNT\" = 0 AND \"CONTROL_ACCOUNT_TYPE\" IS NULL) OR (\"IS_CONTROL_ACCOUNT\" = 1 AND \"CONTROL_ACCOUNT_TYPE\" IN ('AR', 'AP', 'INVENTORY')))"),
            ("CK_GL_ACC_PARENT", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT\" ADD CONSTRAINT \"CK_GL_ACC_PARENT\" CHECK (\"PARENT_ACCOUNT_CODE\" IS NULL OR \"PARENT_ACCOUNT_CODE\" <> \"ACCOUNT_CODE\")"),

            ("PK_GL_ACC_BRANCH", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BRANCH\" ADD CONSTRAINT \"PK_GL_ACC_BRANCH\" PRIMARY KEY (\"ACCOUNT_CODE\", \"BRANCH_ID\")"),
            ("FK_GLAB_ACC", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BRANCH\" ADD CONSTRAINT \"FK_GLAB_ACC\" FOREIGN KEY (\"ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\") ON DELETE CASCADE"),
            ("FK_GLAB_BRANCH", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BRANCH\" ADD CONSTRAINT \"FK_GLAB_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("CK_GL_ACC_BRANCH_ACTIVE", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BRANCH\" ADD CONSTRAINT \"CK_GL_ACC_BRANCH_ACTIVE\" CHECK (\"IS_ACTIVE\" IN (0, 1))"),

            ("FK_GL_CC_PARENT", $"ALTER TABLE \"{schemaName}\".\"GL_COST_CENTER\" ADD CONSTRAINT \"FK_GL_CC_PARENT\" FOREIGN KEY (\"PARENT_COST_CENTER_CODE\") REFERENCES \"{schemaName}\".\"GL_COST_CENTER\" (\"COST_CENTER_CODE\")"),

            ("UX_GL_VOUCHER_TYPE_CODE", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_TYPE\" ADD CONSTRAINT \"UX_GL_VOUCHER_TYPE_CODE\" UNIQUE (\"TYPE_CODE\")"),
            ("UX_GL_VOUCHER_TYPE_KEY", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_TYPE\" ADD CONSTRAINT \"UX_GL_VOUCHER_TYPE_KEY\" UNIQUE (\"TYPE_KEY\")"),
            ("CK_GL_VT_CATEGORY", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_TYPE\" ADD CONSTRAINT \"CK_GL_VT_CATEGORY\" CHECK (\"CATEGORY\" IN ('JOURNAL', 'RECEIPT', 'PAYMENT', 'SYSTEM'))"),
            ("CK_GL_VT_RESET", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_TYPE\" ADD CONSTRAINT \"CK_GL_VT_RESET\" CHECK (\"SERIAL_RESET_POLICY\" IN ('YEARLY', 'MONTHLY', 'CONTINUOUS'))"),

            ("UX_GL_VOUCHER_NO", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_HEADER\" ADD CONSTRAINT \"UX_GL_VOUCHER_NO\" UNIQUE (\"BRANCH_ID\", \"VOUCHER_YEAR\", \"VOUCHER_MONTH\", \"VOUCHER_TYPE\", \"VOUCHER_NO\")"),
            ("FK_GL_VH_BRANCH", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_HEADER\" ADD CONSTRAINT \"FK_GL_VH_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("FK_GL_VD_VH", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_DETAIL\" ADD CONSTRAINT \"FK_GL_VD_VH\" FOREIGN KEY (\"VOUCHER_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"ID\") ON DELETE CASCADE"),
            ("FK_GL_VD_ACC", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_DETAIL\" ADD CONSTRAINT \"FK_GL_VD_ACC\" FOREIGN KEY (\"ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),
            ("FK_GL_VD_CC", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_DETAIL\" ADD CONSTRAINT \"FK_GL_VD_CC\" FOREIGN KEY (\"COST_CENTER_CODE\") REFERENCES \"{schemaName}\".\"GL_COST_CENTER\" (\"COST_CENTER_CODE\")"),
            ("FK_GL_VD_BRANCH", $"ALTER TABLE \"{schemaName}\".\"GL_VOUCHER_DETAIL\" ADD CONSTRAINT \"FK_GL_VD_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("FK_GL_FP_YEAR", $"ALTER TABLE \"{schemaName}\".\"GL_FISCAL_PERIOD\" ADD CONSTRAINT \"FK_GL_FP_YEAR\" FOREIGN KEY (\"FISCAL_YEAR_ID\") REFERENCES \"{schemaName}\".\"SYS_FISCAL_YEAR\" (\"Id\") ON DELETE CASCADE"),
            ("UX_GL_FP_YEAR_NUM", $"ALTER TABLE \"{schemaName}\".\"GL_FISCAL_PERIOD\" ADD CONSTRAINT \"UX_GL_FP_YEAR_NUM\" UNIQUE (\"FISCAL_YEAR_ID\", \"PERIOD_NUMBER\")"),

            ("UX_GL_ACC_BAL_DIM", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BALANCE\" ADD CONSTRAINT \"UX_GL_ACC_BAL_DIM\" UNIQUE (\"ACCOUNT_CODE\", \"BRANCH_ID\", \"FISCAL_YEAR_ID\", \"FISCAL_PERIOD_ID\", \"CURRENCY_ID\")"),
            ("FK_GL_BAL_ACC", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BALANCE\" ADD CONSTRAINT \"FK_GL_BAL_ACC\" FOREIGN KEY (\"ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),
            ("FK_GL_BAL_BR", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BALANCE\" ADD CONSTRAINT \"FK_GL_BAL_BR\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("FK_GL_BAL_FY", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BALANCE\" ADD CONSTRAINT \"FK_GL_BAL_FY\" FOREIGN KEY (\"FISCAL_YEAR_ID\") REFERENCES \"{schemaName}\".\"SYS_FISCAL_YEAR\" (\"Id\") ON DELETE CASCADE"),
            ("FK_GL_BAL_FP", $"ALTER TABLE \"{schemaName}\".\"GL_ACCOUNT_BALANCE\" ADD CONSTRAINT \"FK_GL_BAL_FP\" FOREIGN KEY (\"FISCAL_PERIOD_ID\") REFERENCES \"{schemaName}\".\"GL_FISCAL_PERIOD\" (\"ID\") ON DELETE CASCADE"),

            ("UX_CUSTOMER_CODE", $"ALTER TABLE \"{schemaName}\".\"CUSTOMER\" ADD CONSTRAINT \"UX_CUSTOMER_CODE\" UNIQUE (\"CUSTOMER_CODE\")"),
            ("FK_CUST_AR_ACC", $"ALTER TABLE \"{schemaName}\".\"CUSTOMER\" ADD CONSTRAINT \"FK_CUST_AR_ACC\" FOREIGN KEY (\"AR_CONTROL_ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),
            ("FK_CUST_BRANCH", $"ALTER TABLE \"{schemaName}\".\"CUSTOMER\" ADD CONSTRAINT \"FK_CUST_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),

            ("UX_VENDOR_CODE", $"ALTER TABLE \"{schemaName}\".\"VENDOR\" ADD CONSTRAINT \"UX_VENDOR_CODE\" UNIQUE (\"VENDOR_CODE\")"),
            ("FK_VEND_AP_ACC", $"ALTER TABLE \"{schemaName}\".\"VENDOR\" ADD CONSTRAINT \"FK_VEND_AP_ACC\" FOREIGN KEY (\"AP_CONTROL_ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),
            ("FK_VEND_BRANCH", $"ALTER TABLE \"{schemaName}\".\"VENDOR\" ADD CONSTRAINT \"FK_VEND_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),

            ("FK_AR_SUB_CUST", $"ALTER TABLE \"{schemaName}\".\"AR_SUBLEDGER_TRANSACTION\" ADD CONSTRAINT \"FK_AR_SUB_CUST\" FOREIGN KEY (\"CUSTOMER_CODE\") REFERENCES \"{schemaName}\".\"CUSTOMER\" (\"CUSTOMER_CODE\")"),
            ("FK_AR_SUB_LINE", $"ALTER TABLE \"{schemaName}\".\"AR_SUBLEDGER_TRANSACTION\" ADD CONSTRAINT \"FK_AR_SUB_LINE\" FOREIGN KEY (\"JOURNAL_LINE_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_DETAIL\" (\"ID\") ON DELETE CASCADE"),
            ("FK_AR_SUB_VH", $"ALTER TABLE \"{schemaName}\".\"AR_SUBLEDGER_TRANSACTION\" ADD CONSTRAINT \"FK_AR_SUB_VH\" FOREIGN KEY (\"VOUCHER_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"ID\") ON DELETE CASCADE"),

            ("FK_AP_SUB_VEND", $"ALTER TABLE \"{schemaName}\".\"AP_SUBLEDGER_TRANSACTION\" ADD CONSTRAINT \"FK_AP_SUB_VEND\" FOREIGN KEY (\"VENDOR_CODE\") REFERENCES \"{schemaName}\".\"VENDOR\" (\"VENDOR_CODE\")"),
            ("FK_AP_SUB_LINE", $"ALTER TABLE \"{schemaName}\".\"AP_SUBLEDGER_TRANSACTION\" ADD CONSTRAINT \"FK_AP_SUB_LINE\" FOREIGN KEY (\"JOURNAL_LINE_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_DETAIL\" (\"ID\") ON DELETE CASCADE"),
            ("FK_AP_SUB_VH", $"ALTER TABLE \"{schemaName}\".\"AP_SUBLEDGER_TRANSACTION\" ADD CONSTRAINT \"FK_AP_SUB_VH\" FOREIGN KEY (\"VOUCHER_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"ID\") ON DELETE CASCADE"),

            ("FK_PDC_BRANCH", $"ALTER TABLE \"{schemaName}\".\"GL_PDC_REGISTER\" ADD CONSTRAINT \"FK_PDC_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("FK_PDC_YEAR", $"ALTER TABLE \"{schemaName}\".\"GL_PDC_REGISTER\" ADD CONSTRAINT \"FK_PDC_YEAR\" FOREIGN KEY (\"FISCAL_YEAR_ID\") REFERENCES \"{schemaName}\".\"SYS_FISCAL_YEAR\" (\"Id\") ON DELETE CASCADE"),
            ("FK_PDC_ORIGIN_VH", $"ALTER TABLE \"{schemaName}\".\"GL_PDC_REGISTER\" ADD CONSTRAINT \"FK_PDC_ORIGIN_VH\" FOREIGN KEY (\"ORIGINATING_VOUCHER_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"ID\") ON DELETE SET NULL"),
            ("FK_PDC_CLEAR_VH", $"ALTER TABLE \"{schemaName}\".\"GL_PDC_REGISTER\" ADD CONSTRAINT \"FK_PDC_CLEAR_VH\" FOREIGN KEY (\"CLEARING_VOUCHER_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"ID\") ON DELETE SET NULL"),
            ("FK_PDC_BOUNCE_VH", $"ALTER TABLE \"{schemaName}\".\"GL_PDC_REGISTER\" ADD CONSTRAINT \"FK_PDC_BOUNCE_VH\" FOREIGN KEY (\"BOUNCE_VOUCHER_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"ID\") ON DELETE SET NULL"),

            ("FK_PR_BRANCH", $"ALTER TABLE \"{schemaName}\".\"GL_POSTING_RULE\" ADD CONSTRAINT \"FK_PR_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("FK_PR_DEBIT_ACC", $"ALTER TABLE \"{schemaName}\".\"GL_POSTING_RULE\" ADD CONSTRAINT \"FK_PR_DEBIT_ACC\" FOREIGN KEY (\"DEBIT_ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),
            ("FK_PR_CREDIT_ACC", $"ALTER TABLE \"{schemaName}\".\"GL_POSTING_RULE\" ADD CONSTRAINT \"FK_PR_CREDIT_ACC\" FOREIGN KEY (\"CREDIT_ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),
            ("FK_PR_CC", $"ALTER TABLE \"{schemaName}\".\"GL_POSTING_RULE\" ADD CONSTRAINT \"FK_PR_CC\" FOREIGN KEY (\"DEFAULT_COST_CENTER_CODE\") REFERENCES \"{schemaName}\".\"GL_COST_CENTER\" (\"COST_CENTER_CODE\")"),

            ("UX_BANK_ACC_NUM", $"ALTER TABLE \"{schemaName}\".\"BANK_ACCOUNT\" ADD CONSTRAINT \"UX_BANK_ACC_NUM\" UNIQUE (\"ACCOUNT_NUMBER\")"),
            ("FK_BA_BRANCH", $"ALTER TABLE \"{schemaName}\".\"BANK_ACCOUNT\" ADD CONSTRAINT \"FK_BA_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("FK_BA_GL_ACC", $"ALTER TABLE \"{schemaName}\".\"BANK_ACCOUNT\" ADD CONSTRAINT \"FK_BA_GL_ACC\" FOREIGN KEY (\"GL_ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),

            ("UX_CASH_REG_CODE", $"ALTER TABLE \"{schemaName}\".\"CASH_REGISTER\" ADD CONSTRAINT \"UX_CASH_REG_CODE\" UNIQUE (\"CODE\")"),
            ("FK_CR_BRANCH", $"ALTER TABLE \"{schemaName}\".\"CASH_REGISTER\" ADD CONSTRAINT \"FK_CR_BRANCH\" FOREIGN KEY (\"BRANCH_ID\") REFERENCES \"{schemaName}\".\"SYS_BRANCH\" (\"Id\")"),
            ("FK_CR_GL_ACC", $"ALTER TABLE \"{schemaName}\".\"CASH_REGISTER\" ADD CONSTRAINT \"FK_CR_GL_ACC\" FOREIGN KEY (\"GL_ACCOUNT_CODE\") REFERENCES \"{schemaName}\".\"GL_ACCOUNT\" (\"ACCOUNT_CODE\")"),

            ("FK_BR_ACC", $"ALTER TABLE \"{schemaName}\".\"BANK_RECONCILIATION\" ADD CONSTRAINT \"FK_BR_ACC\" FOREIGN KEY (\"BANK_ACCOUNT_ID\") REFERENCES \"{schemaName}\".\"BANK_ACCOUNT\" (\"ID\") ON DELETE CASCADE"),
            ("FK_BR_YEAR", $"ALTER TABLE \"{schemaName}\".\"BANK_RECONCILIATION\" ADD CONSTRAINT \"FK_BR_YEAR\" FOREIGN KEY (\"FISCAL_YEAR_ID\") REFERENCES \"{schemaName}\".\"SYS_FISCAL_YEAR\" (\"Id\") ON DELETE CASCADE"),
            ("FK_BR_PERIOD", $"ALTER TABLE \"{schemaName}\".\"BANK_RECONCILIATION\" ADD CONSTRAINT \"FK_BR_PERIOD\" FOREIGN KEY (\"FISCAL_PERIOD_ID\") REFERENCES \"{schemaName}\".\"GL_FISCAL_PERIOD\" (\"ID\") ON DELETE CASCADE"),

            ("FK_BSL_RECON", $"ALTER TABLE \"{schemaName}\".\"BANK_STATEMENT_LINE\" ADD CONSTRAINT \"FK_BSL_RECON\" FOREIGN KEY (\"RECONCILIATION_ID\") REFERENCES \"{schemaName}\".\"BANK_RECONCILIATION\" (\"ID\") ON DELETE CASCADE"),
            ("FK_BSL_VD", $"ALTER TABLE \"{schemaName}\".\"BANK_STATEMENT_LINE\" ADD CONSTRAINT \"FK_BSL_VD\" FOREIGN KEY (\"MATCHED_VOUCHER_DETAIL_ID\") REFERENCES \"{schemaName}\".\"GL_VOUCHER_DETAIL\" (\"ID\") ON DELETE SET NULL")
        };

        foreach (var statement in constraintStatements)
        {
            await ExecuteAccountingDdlAsync(connection, $"constraint {statement.Name}", statement.Sql);
        }

        var indexStatements = new (string Name, string Sql)[]
        {
            ("IX_GL_ACCOUNT_PARENT", $"CREATE INDEX \"{schemaName}\".\"IX_GL_ACCOUNT_PARENT\" ON \"{schemaName}\".\"GL_ACCOUNT\" (\"PARENT_ACCOUNT_CODE\")"),
            ("IX_DEV_GL_ACC_OLD_CODE", $"CREATE INDEX \"{schemaName}\".\"IX_DEV_GL_ACC_OLD_CODE\" ON \"{schemaName}\".\"GL_ACCOUNT\" (\"OLD_ACCOUNT_CODE\")"),
            ("IX_GL_ACC_BRANCH_BRANCH", $"CREATE INDEX \"{schemaName}\".\"IX_GL_ACC_BRANCH_BRANCH\" ON \"{schemaName}\".\"GL_ACCOUNT_BRANCH\" (\"BRANCH_ID\")"),
            ("IX_GL_CC_PARENT", $"CREATE INDEX \"{schemaName}\".\"IX_GL_CC_PARENT\" ON \"{schemaName}\".\"GL_COST_CENTER\" (\"PARENT_COST_CENTER_CODE\")"),
            ("IX_GL_VH_DATE", $"CREATE INDEX \"{schemaName}\".\"IX_GL_VH_DATE\" ON \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"VOUCHER_DATE\")"),
            ("IX_GL_VH_TYPE", $"CREATE INDEX \"{schemaName}\".\"IX_GL_VH_TYPE\" ON \"{schemaName}\".\"GL_VOUCHER_HEADER\" (\"VOUCHER_TYPE\")"),
            ("IX_GL_VD_ACC", $"CREATE INDEX \"{schemaName}\".\"IX_GL_VD_ACC\" ON \"{schemaName}\".\"GL_VOUCHER_DETAIL\" (\"ACCOUNT_CODE\")"),
            ("IX_GL_VD_CC", $"CREATE INDEX \"{schemaName}\".\"IX_GL_VD_CC\" ON \"{schemaName}\".\"GL_VOUCHER_DETAIL\" (\"COST_CENTER_CODE\")"),
            ("IX_GL_FP_DATES", $"CREATE INDEX \"{schemaName}\".\"IX_GL_FP_DATES\" ON \"{schemaName}\".\"GL_FISCAL_PERIOD\" (\"START_DATE\", \"END_DATE\")"),
            ("IX_GL_BAL_PERIOD", $"CREATE INDEX \"{schemaName}\".\"IX_GL_BAL_PERIOD\" ON \"{schemaName}\".\"GL_ACCOUNT_BALANCE\" (\"FISCAL_YEAR_ID\", \"FISCAL_PERIOD_ID\", \"BRANCH_ID\")")
        };

        foreach (var statement in indexStatements)
        {
            await ExecuteAccountingDdlAsync(connection, $"index {statement.Name}", statement.Sql);
        }

        var autoRepairParentSql = $"""
            UPDATE "{schemaName}"."GL_ACCOUNT" child
            SET "PARENT_ACCOUNT_CODE" = (
                SELECT parent."ACCOUNT_CODE"
                FROM "{schemaName}"."GL_ACCOUNT" parent
                WHERE parent."ACCOUNT_LEVEL" = child."ACCOUNT_LEVEL" - 1
                  AND child."ACCOUNT_CODE" LIKE parent."ACCOUNT_CODE" || '%'
                  AND ROWNUM = 1
            )
            WHERE child."ACCOUNT_LEVEL" > 1 AND child."PARENT_ACCOUNT_CODE" IS NULL
            """;
        await ExecuteRawAsync(connection, autoRepairParentSql);

        _logger.LogInformation("Accounting schema objects ensured in {SchemaName}", schemaName);
    }

    private static string NormalizeOracleIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Oracle schema identifier is required.", nameof(identifier));

        var normalized = identifier.ToUpperInvariant();
        if (normalized.Length > 128 || normalized[0] is < 'A' or > 'Z')
            throw new ArgumentException("Oracle schema identifier has an invalid format.", nameof(identifier));

        foreach (var character in normalized)
        {
            var isLetter = character is >= 'A' and <= 'Z';
            var isDigit = character is >= '0' and <= '9';
            if (!isLetter && !isDigit && character is not ('_' or '$' or '#'))
                throw new ArgumentException("Oracle schema identifier has an invalid format.", nameof(identifier));
        }

        return normalized;
    }

    private async Task ExecuteAccountingDdlAsync(OracleConnection connection, string objectDescription, string sql)
    {
        try
        {
            await ExecuteRawAsync(connection, sql);
        }
        catch (OracleException ex) when (ex.Number is 955 or 1408 or 2260 or 2261 or 2264 or 2275)
        {
            _logger.LogDebug(
                "Accounting {ObjectDescription} already exists in the requested form or an equivalent form: {Message}",
                objectDescription,
                ex.Message);
        }
    }

    private async Task SeedAccountCategoriesAsync(OracleConnection connection, string schemaName)
    {
        var categories = new (long Id, int Code, string NameAr, string NameEn, string Balance, string Statement, int Order)[]
        {
            (1, 1, "الأصول", "Assets", "D", "BALANCE_SHEET", 1),
            (2, 2, "الالتزامات", "Liabilities", "C", "BALANCE_SHEET", 2),
            (3, 3, "حقوق الملكية", "Equity", "C", "BALANCE_SHEET", 3),
            (4, 4, "الإيرادات", "Revenue", "C", "INCOME_STATEMENT", 4),
            (5, 5, "تكلفة المبيعات", "Cost of Sales", "D", "INCOME_STATEMENT", 5),
            (6, 6, "المصروفات التشغيلية", "Operating Expenses", "D", "INCOME_STATEMENT", 6),
            (7, 7, "الإيرادات الأخرى", "Other Income", "C", "INCOME_STATEMENT", 7),
            (8, 8, "المصروفات الأخرى", "Other Expenses", "D", "INCOME_STATEMENT", 8)
        };

        var mergeSql = $"""
            MERGE INTO "{schemaName}"."ACCOUNT_CATEGORY" target
            USING
            (
                SELECT :id AS "Id",
                       :categoryCode AS "CATEGORY_CODE",
                       :nameAr AS "NAME_AR",
                       :nameEn AS "NAME_EN",
                       :normalBalance AS "NORMAL_BALANCE",
                       :financialStatement AS "FINANCIAL_STATEMENT",
                       :displayOrder AS "DISPLAY_ORDER"
                FROM DUAL
            ) source
            ON (target."Id" = source."Id")
            WHEN MATCHED THEN UPDATE SET
                target."CATEGORY_CODE" = source."CATEGORY_CODE",
                target."NAME_AR" = source."NAME_AR",
                target."NAME_EN" = source."NAME_EN",
                target."NORMAL_BALANCE" = source."NORMAL_BALANCE",
                target."FINANCIAL_STATEMENT" = source."FINANCIAL_STATEMENT",
                target."DISPLAY_ORDER" = source."DISPLAY_ORDER"
            WHEN NOT MATCHED THEN INSERT
            (
                "Id", "CATEGORY_CODE", "NAME_AR", "NAME_EN",
                "NORMAL_BALANCE", "FINANCIAL_STATEMENT", "DISPLAY_ORDER"
            )
            VALUES
            (
                source."Id", source."CATEGORY_CODE", source."NAME_AR", source."NAME_EN",
                source."NORMAL_BALANCE", source."FINANCIAL_STATEMENT", source."DISPLAY_ORDER"
            )
            """;

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var category in categories)
            {
                await using var cmd = connection.CreateCommand();
                cmd.BindByName = true;
                cmd.CommandText = mergeSql;
                cmd.Transaction = transaction;
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = category.Id });
                cmd.Parameters.Add(new OracleParameter("categoryCode", OracleDbType.Int32) { Value = category.Code });
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 200) { Value = category.NameAr });
                cmd.Parameters.Add(new OracleParameter("nameEn", OracleDbType.NVarchar2, 200) { Value = category.NameEn });
                cmd.Parameters.Add(new OracleParameter("normalBalance", OracleDbType.NVarchar2, 1) { Value = category.Balance });
                cmd.Parameters.Add(new OracleParameter("financialStatement", OracleDbType.NVarchar2, 20) { Value = category.Statement });
                cmd.Parameters.Add(new OracleParameter("displayOrder", OracleDbType.Int32) { Value = category.Order });
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                _logger.LogWarning(rollbackException, "Failed to roll back account-category seed transaction for {SchemaName}", schemaName);
            }

            throw;
        }
    }

    private async Task SeedVoucherTypesAsync(OracleConnection connection, string schemaName)
    {
        var voucherTypes = new (int Code, string Key, string NameAr, string NameEn, string Prefix, string Category, string Policy, int Review, int Manual, int System, int Order)[]
        {
            (101, "JV", "قيد يومية عام", "General Journal Voucher", "JV", "JOURNAL", "MONTHLY", 1, 1, 1, 1),
            (102, "RV", "سند قبض", "Receipt Voucher", "RV", "RECEIPT", "MONTHLY", 1, 1, 1, 2),
            (103, "PV", "سند صرف", "Payment Voucher", "PV", "PAYMENT", "MONTHLY", 1, 1, 1, 3),
            (201, "SALES", "قيد مبيعات آلي", "Sales Automated Journal", "INV", "SYSTEM", "MONTHLY", 0, 0, 1, 4),
            (202, "PURCHASE", "قيد مشتريات آلي", "Purchases Automated Journal", "PUR", "SYSTEM", "MONTHLY", 0, 0, 1, 5),
            (301, "CLOSING", "قيد إقفال سنوي", "Fiscal Year Closing Journal", "CLS", "SYSTEM", "YEARLY", 1, 0, 1, 6),
            (302, "OPENING", "قيد أرصدة افتتاحية", "Opening Balance Journal", "OB", "JOURNAL", "YEARLY", 1, 1, 1, 7)
        };

        var mergeSql = $"""
            MERGE INTO "{schemaName}"."GL_VOUCHER_TYPE" target
            USING
            (
                SELECT :typeCode AS "TYPE_CODE",
                       :typeKey AS "TYPE_KEY",
                       :nameAr AS "NAME_AR",
                       :nameEn AS "NAME_EN",
                       :prefix AS "PREFIX",
                       :category AS "CATEGORY",
                       :serialResetPolicy AS "SERIAL_RESET_POLICY",
                       :requiresReview AS "REQUIRES_REVIEW",
                       :allowManualEntry AS "ALLOW_MANUAL_ENTRY",
                       :isSystem AS "IS_SYSTEM",
                       :displayOrder AS "DISPLAY_ORDER",
                       'SYSTEM' AS "CREATION_USER"
                FROM DUAL
            ) source
            ON (target."TYPE_CODE" = source."TYPE_CODE")
            WHEN MATCHED THEN UPDATE SET
                target."NAME_AR" = source."NAME_AR",
                target."NAME_EN" = source."NAME_EN",
                target."PREFIX" = source."PREFIX",
                target."CATEGORY" = source."CATEGORY",
                target."SERIAL_RESET_POLICY" = source."SERIAL_RESET_POLICY",
                target."REQUIRES_REVIEW" = source."REQUIRES_REVIEW",
                target."ALLOW_MANUAL_ENTRY" = source."ALLOW_MANUAL_ENTRY",
                target."IS_SYSTEM" = source."IS_SYSTEM",
                target."DISPLAY_ORDER" = source."DISPLAY_ORDER"
            WHEN NOT MATCHED THEN INSERT
            (
                "TYPE_CODE", "TYPE_KEY", "NAME_AR", "NAME_EN", "PREFIX", "CATEGORY",
                "SERIAL_RESET_POLICY", "REQUIRES_REVIEW", "ALLOW_MANUAL_ENTRY", "IS_SYSTEM", "DISPLAY_ORDER", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
            )
            VALUES
            (
                source."TYPE_CODE", source."TYPE_KEY", source."NAME_AR", source."NAME_EN", source."PREFIX", source."CATEGORY",
                source."SERIAL_RESET_POLICY", source."REQUIRES_REVIEW", source."ALLOW_MANUAL_ENTRY", source."IS_SYSTEM", source."DISPLAY_ORDER", 1, source."CREATION_USER", CURRENT_TIMESTAMP
            )
            """;

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var vt in voucherTypes)
            {
                await using var cmd = connection.CreateCommand();
                cmd.BindByName = true;
                cmd.CommandText = mergeSql;
                cmd.Transaction = transaction;
                cmd.Parameters.Add(new OracleParameter("typeCode", OracleDbType.Int32) { Value = vt.Code });
                cmd.Parameters.Add(new OracleParameter("typeKey", OracleDbType.NVarchar2, 20) { Value = vt.Key });
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 200) { Value = vt.NameAr });
                cmd.Parameters.Add(new OracleParameter("nameEn", OracleDbType.NVarchar2, 200) { Value = vt.NameEn });
                cmd.Parameters.Add(new OracleParameter("prefix", OracleDbType.NVarchar2, 10) { Value = vt.Prefix });
                cmd.Parameters.Add(new OracleParameter("category", OracleDbType.NVarchar2, 20) { Value = vt.Category });
                cmd.Parameters.Add(new OracleParameter("serialResetPolicy", OracleDbType.NVarchar2, 20) { Value = vt.Policy });
                cmd.Parameters.Add(new OracleParameter("requiresReview", OracleDbType.Int32) { Value = vt.Review });
                cmd.Parameters.Add(new OracleParameter("allowManualEntry", OracleDbType.Int32) { Value = vt.Manual });
                cmd.Parameters.Add(new OracleParameter("isSystem", OracleDbType.Int32) { Value = vt.System });
                cmd.Parameters.Add(new OracleParameter("displayOrder", OracleDbType.Int32) { Value = vt.Order });
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                _logger.LogWarning(rollbackException, "Failed to roll back voucher-type seed transaction for {SchemaName}", schemaName);
            }

            throw;
        }
    }

    private async Task SeedCostCentersAsync(OracleConnection connection, string schemaName)
    {
        var costCenters = new (string Code, string? ParentCode, string NameAr, string NameEn, int Level, string Type, int IsPostable)[]
        {
            ("100", null, "الإدارة العامة والخدمات المساندة", "General Administration & Support", 1, "HEADER", 0),
            ("101", "100", "قسم تقنية المعلومات", "Information Technology Dept", 2, "DETAIL", 1),
            ("102", "100", "قسم الموارد البشرية", "Human Resources Dept", 2, "DETAIL", 1),
            ("200", null, "إدارة المبيعات والتسويق", "Sales & Marketing Administration", 1, "HEADER", 0),
            ("201", "200", "قسم التسويق والإعلانات", "Marketing & Advertising Dept", 2, "DETAIL", 1),
            ("202", "200", "قسم المبيعات المباشرة", "Direct Sales Dept", 2, "DETAIL", 1),
            ("300", null, "إدارة العمليات والتشغيل", "Operations & Maintenance Administration", 1, "HEADER", 0),
            ("301", "300", "قسم الخدمات اللوجستية والنقل", "Logistics & Transport Dept", 2, "DETAIL", 1)
        };

        var mergeSql = $"""
            MERGE INTO "{schemaName}"."GL_COST_CENTER" target
            USING
            (
                SELECT :costCenterCode AS "COST_CENTER_CODE",
                       :parentCode AS "PARENT_COST_CENTER_CODE",
                       :nameAr AS "NAME_AR",
                       :nameEn AS "NAME_EN",
                       :ccLevel AS "COST_CENTER_LEVEL",
                       :ccType AS "COST_CENTER_TYPE",
                       :isPostable AS "IS_POSTABLE",
                       'SYSTEM' AS "CREATION_USER"
                FROM DUAL
            ) source
            ON (target."COST_CENTER_CODE" = source."COST_CENTER_CODE")
            WHEN MATCHED THEN UPDATE SET
                target."NAME_AR" = source."NAME_AR",
                target."NAME_EN" = source."NAME_EN",
                target."COST_CENTER_LEVEL" = source."COST_CENTER_LEVEL",
                target."COST_CENTER_TYPE" = source."COST_CENTER_TYPE",
                target."IS_POSTABLE" = source."IS_POSTABLE"
            WHEN NOT MATCHED THEN INSERT
            (
                "COST_CENTER_CODE", "PARENT_COST_CENTER_CODE", "NAME_AR", "NAME_EN",
                "COST_CENTER_LEVEL", "COST_CENTER_TYPE", "IS_POSTABLE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
            )
            VALUES
            (
                source."COST_CENTER_CODE", source."PARENT_COST_CENTER_CODE", source."NAME_AR", source."NAME_EN",
                source."COST_CENTER_LEVEL", source."COST_CENTER_TYPE", source."IS_POSTABLE", 1, source."CREATION_USER", CURRENT_TIMESTAMP
            )
            """;

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var cc in costCenters)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.BindByName = true;
                cmd.CommandText = mergeSql;
                cmd.Parameters.Add(new OracleParameter("costCenterCode", OracleDbType.NVarchar2, 50) { Value = cc.Code });
                cmd.Parameters.Add(new OracleParameter("parentCode", OracleDbType.NVarchar2, 50) { Value = (object?)cc.ParentCode ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 200) { Value = cc.NameAr });
                cmd.Parameters.Add(new OracleParameter("nameEn", OracleDbType.NVarchar2, 200) { Value = cc.NameEn });
                cmd.Parameters.Add(new OracleParameter("ccLevel", OracleDbType.Int32) { Value = cc.Level });
                cmd.Parameters.Add(new OracleParameter("ccType", OracleDbType.NVarchar2, 20) { Value = cc.Type });
                cmd.Parameters.Add(new OracleParameter("isPostable", OracleDbType.Int32) { Value = cc.IsPostable });
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                _logger.LogWarning(rollbackException, "Failed to roll back cost-center seed transaction for {SchemaName}", schemaName);
            }

            throw;
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
        _logger.LogInformation("Upgrading all existing tenant schemas against template {SourceSchema}", _devSchemaName);
        
        await using var masterConn = await OpenMasterConnectionAsync();

        // Get all company schemas
        var schemas = new List<string>();
        try
        {
            await using var cmd = masterConn.CreateCommand();
            cmd.CommandText = "SELECT COMPANY_SCHEMA FROM \"THINKON_ERP\".\"SYS_COMPANY\" WHERE COMPANY_SCHEMA IS NOT NULL";
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

        foreach (var schema in schemas)
        {
            // Skip the template schema itself if it is in the list
            if (string.Equals(schema, _devSchemaName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                _logger.LogInformation("Syncing schema: {Schema}", schema);
                await CloneFromDeveloperSchemaAsync(masterConn, schema);
                await SyncTableColumnsSchemaDiffAsync(masterConn, schema);
                await EnsureAccountingSchemaAsync(masterConn, schema);

                // Regenerate synonyms & table access grants to ensure everything is correct
                await GrantTenantTableAccessAsync(schema, schema);
                await CreateGlobalSynonymsAsync(schema, schema);
                
                _logger.LogInformation("Upgraded and synced tenant schema: {Schema}", schema);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upgrade tenant schema: {Schema}", schema);
            }
        }
    }

    public async Task SyncTenantSchemaAsync(string schemaName, string schemaPassword)
    {
        schemaName = schemaName.ToUpperInvariant();
        _logger.LogInformation("Syncing tenant schema {SchemaName} with template {SourceSchema}", schemaName, _devSchemaName);
        
        await using var masterConn = await OpenMasterConnectionAsync();
        await CloneFromDeveloperSchemaAsync(masterConn, schemaName);
        await SyncTableColumnsSchemaDiffAsync(masterConn, schemaName);
        await EnsureAccountingSchemaAsync(masterConn, schemaName);
        
        // Ensure access privileges and synonyms are up to date
        await GrantTenantTableAccessAsync(schemaName, schemaPassword);
        await CreateGlobalSynonymsAsync(schemaName, schemaPassword);
    }

    public async Task ProvisionDeveloperSchemaAsync()
    {
        _logger.LogInformation("Ensuring developer template schema exists: {SchemaName}", _devSchemaName);

        await using var masterConn = await OpenMasterConnectionAsync();

        // Quick check: if DEV_TEMPLATE schema already exists and has GL_ACCOUNT table populated, skip full heavy provisioning
        try
        {
            await using var checkCmd = masterConn.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(1) FROM \"{_devSchemaName}\".\"GL_ACCOUNT\"";
            var countObj = await checkCmd.ExecuteScalarAsync();
            var count = Convert.ToInt64(countObj);
            if (count > 0)
            {
                _logger.LogInformation("Developer template schema {SchemaName} already provisioned ({Count} accounts found). Skipping startup auto-provisioning.", _devSchemaName, count);
                return;
            }
        }
        catch
        {
            // Table doesn't exist yet, proceed with full provisioning below
        }

        // Enable Oracle 12c+ script mode
        await ExecuteRawAsync(masterConn, "ALTER SESSION SET \"_ORACLE_SCRIPT\" = TRUE");

        // 1. Create the Oracle user for developer template if not exists
        try
        {
            await ExecuteRawAsync(masterConn, $"CREATE USER \"{_devSchemaName}\" IDENTIFIED BY \"{_devSchemaPassword}\"");
            _logger.LogInformation("Developer template user created: {SchemaName}", _devSchemaName);
        }
        catch (OracleException ex) when (ex.Number == 1920)
        {
            _logger.LogDebug("Developer template schema already exists: {SchemaName}", _devSchemaName);
        }

        // 2. Grant privileges and tablespace quota
        await ExecuteRawAsync(masterConn,
            $"GRANT CONNECT, RESOURCE, CREATE SESSION, CREATE TABLE, CREATE VIEW, CREATE SEQUENCE, CREATE PROCEDURE, CREATE TRIGGER, CREATE SYNONYM TO \"{_devSchemaName}\"");
        await ExecuteRawAsync(masterConn,
            $"ALTER USER \"{_devSchemaName}\" QUOTA UNLIMITED ON USERS");
        await ExecuteRawAsync(masterConn,
            $"ALTER USER \"{_devSchemaName}\" ACCOUNT UNLOCK");

        // 3. Bootstrap tables from EF model into the template schema
        await BootstrapTablesFromEfModelAsync(masterConn, _devSchemaName);

        // 4. Apply accounting-specific Oracle DDL that the lightweight EF bootstrap does not
        // generate (foreign keys, check constraints, defaults, and indexes), then seed the
        // fixed tenant-local account categories.
        await EnsureAccountingSchemaAsync(masterConn, _devSchemaName);
        
        // 5. Create synonyms for global tables in the developer template schema so it has full connectivity
        await CreateGlobalSynonymsAsync(_devSchemaName, _devSchemaPassword);

        // 6. Seed the template schema with comprehensive test data
        try
        {
            await SeedDeveloperTemplateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed developer template schema during provisioning");
        }

        _logger.LogInformation("Developer template schema provisioning/verification completed: {SchemaName}", _devSchemaName);
    }

    public async Task SeedDeveloperTemplateAsync()
    {
        _logger.LogInformation("Seeding developer template schema: {SchemaName}", _devSchemaName);

        string scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", "Scripts", "92_Seed_Developer_Template.sql");
        if (!File.Exists(scriptPath))
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "Database", "Scripts", "92_Seed_Developer_Template.sql");
                if (File.Exists(candidate))
                {
                    scriptPath = candidate;
                    break;
                }
                dir = Path.GetDirectoryName(dir);
            }
        }

        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("Seed script not found: {Path}", scriptPath);
            return;
        }

        var scriptContent = await File.ReadAllTextAsync(scriptPath);
        var statements = scriptContent
            .Split(new[] { "\r\n/\r\n", "\n/\n", "\r/\r", "\r\n/\n", "\n/\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var hashedPassword = _passwordHashingService.HashPassword("Password@123");

        await using var conn = await OpenTenantConnectionAsync(_devSchemaName, _devSchemaPassword);

        foreach (var stmt in statements)
        {
            var query = stmt.Replace("'TEMP_HASH'", $"'{hashedPassword}'", StringComparison.OrdinalIgnoreCase);
            
            // Clean up slash characters if they are still at the end of the query
            if (query.EndsWith("/", StringComparison.Ordinal))
            {
                query = query.Substring(0, query.Length - 1).Trim();
            }

            try
            {
                await ExecuteRawAsync(conn, query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run seed statement: {Stmt}", query.Length > 200 ? query.Substring(0, 200) + "..." : query);
                throw;
            }
        }

        // Reset identity sequences for tables with hardcoded IDs to prevent duplicate key errors on EF insertions
        var tablesToReset = new[]
        {
            "SYS_ROLE", "SYS_BRANCH", "SYS_USERS", "SYS_USERS_ROLES", "SYS_USER_BRANCHES",
            "SYS_FISCAL_YEAR", "SYS_RETENTION_POLICIES", "SYS_TICKET_PRIORITY", "SYS_TICKET_STATUS",
            "SYS_TICKET_TYPE", "SYS_TICKET_CATEGORY", "SYS_TICKET_CONFIG", "SYS_REQUEST_TICKET",
            "SYS_TICKET_COMMENT", "SYS_SAVED_SEARCH"
        };
        
        foreach (var table in tablesToReset)
        {
            try
            {
                try
                {
                    await ExecuteRawAsync(conn, $"ALTER TABLE \"{_devSchemaName}\".\"{table}\" MODIFY \"Id\" GENERATED BY DEFAULT AS IDENTITY (START WITH LIMIT VALUE)");
                    _logger.LogDebug("Reset identity sequence for table {Table} using START WITH LIMIT VALUE", table);
                }
                catch (Exception exLimit)
                {
                    _logger.LogDebug(exLimit, "START WITH LIMIT VALUE failed for table {Table}, falling back to dynamic start value", table);
                    long nextId = 1;
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT NVL(MAX(\"Id\"), 0) FROM \"{_devSchemaName}\".\"{table}\"";
                        var maxVal = await cmd.ExecuteScalarAsync();
                        if (maxVal != null && maxVal != DBNull.Value)
                        {
                            nextId = Convert.ToInt64(maxVal) + 1;
                        }
                    }

                    await ExecuteRawAsync(conn, $"ALTER TABLE \"{_devSchemaName}\".\"{table}\" MODIFY \"Id\" GENERATED BY DEFAULT AS IDENTITY (START WITH {nextId})");
                    _logger.LogDebug("Reset identity sequence for table {Table} to next ID {NextId}", table, nextId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reset identity sequence for table {Table}", table);
            }
        }

        _logger.LogInformation("Successfully seeded developer template schema: {SchemaName}", _devSchemaName);
    }

    private async Task CloneFromDeveloperSchemaAsync(OracleConnection connection, string targetSchema)
    {
        _logger.LogInformation("Cloning schema structure from {SourceSchema} to {TargetSchema}", _devSchemaName, targetSchema);

        var objects = new List<(string Name, string Type)>();
        
        var query = @"
            SELECT OBJECT_NAME, OBJECT_TYPE 
            FROM ALL_OBJECTS 
            WHERE OWNER = :sourceSchema 
              AND (
                (OBJECT_TYPE = 'TABLE' AND OBJECT_NAME NOT LIKE 'BIN$%') OR
                (OBJECT_TYPE = 'INDEX' AND OBJECT_NAME NOT LIKE 'SYS_%') OR
                (OBJECT_TYPE = 'SEQUENCE' AND OBJECT_NAME NOT LIKE 'ISEQ$$%') OR
                (OBJECT_TYPE IN ('VIEW', 'PROCEDURE', 'FUNCTION', 'PACKAGE', 'PACKAGE BODY', 'TRIGGER', 'TYPE'))
              )
            ORDER BY 
              CASE OBJECT_TYPE 
                WHEN 'TYPE' THEN 1
                WHEN 'SEQUENCE' THEN 2
                WHEN 'TABLE' THEN 3
                WHEN 'INDEX' THEN 4
                WHEN 'VIEW' THEN 5
                WHEN 'FUNCTION' THEN 6
                WHEN 'PROCEDURE' THEN 7
                WHEN 'PACKAGE' THEN 8
                WHEN 'PACKAGE BODY' THEN 9
                WHEN 'TRIGGER' THEN 10
                ELSE 11
              END";

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.Add(new OracleParameter("sourceSchema", _devSchemaName));
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                objects.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        _logger.LogInformation("Found {Count} objects to clone from {SourceSchema}", objects.Count, _devSchemaName);

        // Set DBMS_METADATA session transform options for portable DDL
        try
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "BEGIN DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM, 'SEGMENT_ATTRIBUTES', FALSE); END;";
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "BEGIN DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM, 'TABLESPACE', FALSE); END;";
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "BEGIN DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM, 'STORAGE', FALSE); END;";
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set DBMS_METADATA session transform parameters. Output DDL may contain storage clauses.");
        }

        foreach (var obj in objects)
        {
            if (GlobalTables.Contains(obj.Name))
                continue;

            if (!TenantOnlyTables.Contains(obj.Name) && obj.Type == "TABLE")
                continue;

            _logger.LogDebug("Retrieving DDL for {Type} {Name} from {SourceSchema}", obj.Type, obj.Name, _devSchemaName);

            string ddl = "";
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT DBMS_METADATA.GET_DDL(:objType, :objName, :sourceSchema) FROM DUAL";
                cmd.Parameters.Add(new OracleParameter("objType", obj.Type == "PACKAGE BODY" ? "PACKAGE_BODY" : obj.Type));
                cmd.Parameters.Add(new OracleParameter("objName", obj.Name));
                cmd.Parameters.Add(new OracleParameter("sourceSchema", _devSchemaName));

                var result = await cmd.ExecuteScalarAsync();
                ddl = result?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get DDL for {Type} {Name}", obj.Type, obj.Name);
                continue;
            }

            if (string.IsNullOrWhiteSpace(ddl))
                continue;

            // Retarget DDL to the target schema name
            var pattern = $"\"{_devSchemaName}\".";
            var replacement = $"\"{targetSchema}\".";
            ddl = ddl.Replace(pattern, replacement, StringComparison.OrdinalIgnoreCase);
            ddl = ddl.Replace($"\"{_devSchemaName}\"", $"\"{targetSchema}\"", StringComparison.OrdinalIgnoreCase);
            ddl = System.Text.RegularExpressions.Regex.Replace(ddl, @"SHARING\s*=\s*\w+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // For programmable routines (VIEW, FUNCTION, PROCEDURE, PACKAGE, TRIGGER, TYPE), ensure CREATE OR REPLACE
            if (obj.Type is "VIEW" or "FUNCTION" or "PROCEDURE" or "PACKAGE" or "PACKAGE BODY" or "TRIGGER" or "TYPE")
            {
                if (ddl.TrimStart().StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase) &&
                    !ddl.TrimStart().StartsWith("CREATE OR REPLACE", StringComparison.OrdinalIgnoreCase))
                {
                    var trimmed = ddl.TrimStart();
                    ddl = "CREATE OR REPLACE " + trimmed.Substring(7);
                }
            }

            _logger.LogDebug("Executing DDL for {Type} {TargetSchema}.{Name}", obj.Type, targetSchema, obj.Name);
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (OracleException ex) when (ex.Number == 955 || ex.Number == 2261 || ex.Number == 2264 || ex.Number == 1917)
            {
                _logger.LogDebug("Object/constraint {Name} already exists or is redundant: {Msg}", obj.Name, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating cloned {Type} {TargetSchema}.{Name}. DDL was: {Ddl}", obj.Type, targetSchema, obj.Name, ddl);
            }
        }
    }

    private async Task SyncTableColumnsSchemaDiffAsync(OracleConnection connection, string targetSchema)
    {
        _logger.LogInformation("Performing comprehensive Schema Diff for table columns between {SourceSchema} and {TargetSchema}", _devSchemaName, targetSchema);

        var diffSql = $@"
            DECLARE
                v_sql VARCHAR2(4000);
            BEGIN
                -- 1. Add missing columns to targetSchema tables
                FOR r IN (
                    SELECT 
                        src.table_name,
                        src.column_name,
                        src.data_type,
                        src.data_length,
                        src.data_precision,
                        src.data_scale,
                        src.nullable,
                        src.data_default
                    FROM all_tab_cols src
                    JOIN all_tables t ON src.owner = t.owner AND src.table_name = t.table_name
                    LEFT JOIN all_tab_cols tgt 
                        ON tgt.owner = '{targetSchema}' 
                       AND tgt.table_name = src.table_name 
                       AND tgt.column_name = src.column_name
                    WHERE src.owner = '{_devSchemaName}'
                      AND tgt.column_name IS NULL
                      AND src.table_name IN (
                          SELECT table_name FROM all_tables WHERE owner = '{targetSchema}'
                      )
                    ORDER BY src.table_name, src.column_id
                ) LOOP
                    v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" ADD ""' || r.column_name || '"" ' || r.data_type;
                    
                    IF r.data_type IN ('VARCHAR2', 'NVARCHAR2', 'RAW', 'CHAR', 'NCHAR') THEN
                        v_sql := v_sql || '(' || r.data_length || ')';
                    ELSIF r.data_type = 'NUMBER' AND r.data_precision IS NOT NULL THEN
                        IF r.data_scale IS NOT NULL AND r.data_scale > 0 THEN
                            v_sql := v_sql || '(' || r.data_precision || ',' || r.data_scale || ')';
                        ELSE
                            v_sql := v_sql || '(' || r.data_precision || ')';
                        END IF;
                    END IF;

                    IF r.data_default IS NOT NULL THEN
                        v_sql := v_sql || ' DEFAULT ' || TRIM(r.data_default);
                    END IF;

                    IF r.nullable = 'N' AND r.data_default IS NOT NULL THEN
                        v_sql := v_sql || ' NOT NULL';
                    ELSE
                        v_sql := v_sql || ' NULL';
                    END IF;

                    BEGIN
                        EXECUTE IMMEDIATE v_sql;
                    EXCEPTION WHEN OTHERS THEN 
                        BEGIN
                            v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" ADD ""' || r.column_name || '"" ' || r.data_type;
                            IF r.data_type IN ('VARCHAR2', 'NVARCHAR2', 'RAW', 'CHAR', 'NCHAR') THEN
                                v_sql := v_sql || '(' || r.data_length || ')';
                            ELSIF r.data_type = 'NUMBER' AND r.data_precision IS NOT NULL THEN
                                IF r.data_scale IS NOT NULL AND r.data_scale > 0 THEN
                                    v_sql := v_sql || '(' || r.data_precision || ',' || r.data_scale || ')';
                                ELSE
                                    v_sql := v_sql || '(' || r.data_precision || ')';
                                END IF;
                            END IF;
                            v_sql := v_sql || ' NULL';
                            EXECUTE IMMEDIATE v_sql;
                        EXCEPTION WHEN OTHERS THEN NULL;
                        END;
                    END;
                END LOOP;

                -- 2. Expand mismatched column lengths if DEV_TEMPLATE column size is larger
                FOR r IN (
                    SELECT 
                        src.table_name,
                        src.column_name,
                        src.data_type,
                        src.data_length AS src_length,
                        tgt.data_length AS tgt_length
                    FROM all_tab_cols src
                    JOIN all_tab_cols tgt 
                        ON tgt.owner = '{targetSchema}' 
                       AND tgt.table_name = src.table_name 
                       AND tgt.column_name = src.column_name
                    WHERE src.owner = '{_devSchemaName}'
                      AND src.data_type IN ('VARCHAR2', 'NVARCHAR2', 'CHAR', 'NCHAR')
                      AND src.data_type = tgt.data_type
                      AND src.data_length > tgt.data_length
                ) LOOP
                    BEGIN
                        EXECUTE IMMEDIATE 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" ' || r.data_type || '(' || r.src_length || '))';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END LOOP;
            END;";

        await ExecuteRawAsync(connection, diffSql);
    }

    public async Task<(long BranchId, long FiscalYearId)> ProvisionTenantBranchAndFiscalYearAsync(
        string schemaName,
        string schemaPassword,
        long companyId,
        string? branchNameAr, string? branchNameEn,
        string? branchPhone, string? branchMobile,
        string? branchFax, string? branchEmail,
        string? taxNumber, int defaultLang,
        long? baseCurrencyId, int roundingRules,
        string? branchLogoPath, string creationUser)
    {
        schemaName = schemaName.ToUpperInvariant();
        _logger.LogInformation("Provisioning tenant branch and fiscal year in schema: {SchemaName}", schemaName);

        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, schemaPassword);

        // 1. Insert Branch
        var branchSql = $@"
            INSERT INTO ""{schemaName}"".""SYS_BRANCH"" 
            (""COMPANY_ID"", ""NAME_AR"", ""NAME_EN"", ""PHONE"", ""MOBILE"", ""FAX"", ""EMAIL"", ""IS_HEAD_BRANCH"", ""TAX_NUMBER"", ""DEFAULT_LANG"", ""BASE_CURRENCY_ID"", ""ROUNDING_RULES"", ""BRANCH_LOGO_PATH"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
            VALUES (:companyId, :nameAr, :nameEn, :phone, :mobile, :fax, :email, 1, :taxNumber, :defaultLang, :baseCurrencyId, :roundingRules, :logoPath, 1, :creationUser, SYSDATE)
            RETURNING ""Id"" INTO :branchId";

        await using var branchCmd = tenantConn.CreateCommand();
        branchCmd.CommandText = branchSql;
        branchCmd.Parameters.Add(new OracleParameter("companyId", companyId));
        branchCmd.Parameters.Add(new OracleParameter("nameAr", branchNameAr ?? "Default Branch"));
        branchCmd.Parameters.Add(new OracleParameter("nameEn", branchNameEn ?? "Default Branch"));
        branchCmd.Parameters.Add(new OracleParameter("phone", (object?)branchPhone ?? DBNull.Value));
        branchCmd.Parameters.Add(new OracleParameter("mobile", (object?)branchMobile ?? DBNull.Value));
        branchCmd.Parameters.Add(new OracleParameter("fax", (object?)branchFax ?? DBNull.Value));
        branchCmd.Parameters.Add(new OracleParameter("email", (object?)branchEmail ?? DBNull.Value));
        branchCmd.Parameters.Add(new OracleParameter("taxNumber", (object?)taxNumber ?? DBNull.Value));
        branchCmd.Parameters.Add(new OracleParameter("defaultLang", defaultLang));
        branchCmd.Parameters.Add(new OracleParameter("baseCurrencyId", baseCurrencyId ?? 1L));
        branchCmd.Parameters.Add(new OracleParameter("roundingRules", roundingRules));
        branchCmd.Parameters.Add(new OracleParameter("logoPath", (object?)branchLogoPath ?? DBNull.Value));
        branchCmd.Parameters.Add(new OracleParameter("creationUser", creationUser));

        var branchIdParam = new OracleParameter("branchId", OracleDbType.Decimal)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        branchCmd.Parameters.Add(branchIdParam);
        await branchCmd.ExecuteNonQueryAsync();

        var branchId = Convert.ToInt64(branchIdParam.Value.ToString());
        _logger.LogInformation("Successfully created branch with ID {BranchId} in schema {SchemaName}", branchId, schemaName);

        // 2. Insert Fiscal Year
        var fyCode = $"FY{DateTime.Now.Year}";
        var fyNameAr = $"السنة المالية {DateTime.Now.Year}";
        var fyNameEn = $"Fiscal Year {DateTime.Now.Year}";
        var startDate = new DateTime(DateTime.Now.Year, 1, 1);
        var endDate = new DateTime(DateTime.Now.Year, 12, 31);

        var fySql = $@"
            INSERT INTO ""{schemaName}"".""SYS_FISCAL_YEAR""
            (""COMPANY_ID"", ""BRANCH_ID"", ""FISCAL_YEAR_CODE"", ""NAME_AR"", ""NAME_EN"", ""START_DATE"", ""END_DATE"", ""IS_CLOSED"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
            VALUES (:companyId, :branchId, :fyCode, :nameAr, :nameEn, :startDate, :endDate, 0, 1, :creationUser, SYSDATE)
            RETURNING ""Id"" INTO :fyId";

        await using var fyCmd = tenantConn.CreateCommand();
        fyCmd.CommandText = fySql;
        fyCmd.Parameters.Add(new OracleParameter("companyId", companyId));
        fyCmd.Parameters.Add(new OracleParameter("branchId", branchId));
        fyCmd.Parameters.Add(new OracleParameter("fyCode", fyCode));
        fyCmd.Parameters.Add(new OracleParameter("nameAr", fyNameAr));
        fyCmd.Parameters.Add(new OracleParameter("nameEn", fyNameEn));
        fyCmd.Parameters.Add(new OracleParameter("startDate", startDate));
        fyCmd.Parameters.Add(new OracleParameter("endDate", endDate));
        fyCmd.Parameters.Add(new OracleParameter("creationUser", creationUser));

        var fyIdParam = new OracleParameter("fyId", OracleDbType.Decimal)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        fyCmd.Parameters.Add(fyIdParam);
        await fyCmd.ExecuteNonQueryAsync();

        var fiscalYearId = Convert.ToInt64(fyIdParam.Value.ToString());
        _logger.LogInformation("Successfully created fiscal year with ID {FiscalYearId} in schema {SchemaName}", fiscalYearId, schemaName);

        return (branchId, fiscalYearId);
    }

    public async Task UpdateTenantBranchLogoPathAsync(string schemaName, string schemaPassword, long branchId, string? logoPath, string updateUser)
    {
        schemaName = schemaName.ToUpperInvariant();
        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, schemaPassword);
        await using var cmd = tenantConn.CreateCommand();
        cmd.CommandText = $@"
            UPDATE ""{schemaName}"".""SYS_BRANCH""
            SET ""BRANCH_LOGO_PATH"" = :logoPath, ""UPDATE_USER"" = :updateUser, ""UPDATE_DATE"" = SYSDATE
            WHERE ""Id"" = :branchId";
        cmd.Parameters.Add(new OracleParameter("logoPath", (object?)logoPath ?? DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("updateUser", updateUser));
        cmd.Parameters.Add(new OracleParameter("branchId", branchId));
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Updated branch logo path in tenant schema {SchemaName} for branch ID {BranchId}", schemaName, branchId);
    }

    public async Task<(string? BranchNameEn, string? BranchNameAr, string? BranchLogoPath)> GetBranchDetailsAsync(string schemaName, long branchId)
    {
        if (string.IsNullOrEmpty(schemaName) || branchId <= 0) return (null, null, null);
        try
        {
            await using var conn = await OpenMasterConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT NAME_EN, NAME_AR, BRANCH_LOGO_PATH
                FROM ""{schemaName.ToUpperInvariant()}"".""SYS_BRANCH""
                WHERE ""Id"" = :branchId";
            cmd.Parameters.Add(new OracleParameter("branchId", branchId));
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var nameEn = reader.IsDBNull(0) ? null : reader.GetString(0);
                var nameAr = reader.IsDBNull(1) ? null : reader.GetString(1);
                var logoPath = reader.IsDBNull(2) ? null : reader.GetString(2);
                return (nameEn, nameAr, logoPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch branch details for schema {Schema}, branchId {BranchId}", schemaName, branchId);
        }
        return (null, null, null);
    }
}
