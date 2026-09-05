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

        // Ensure accounting and HR tables, constraints, indexes, and reference data exist even when
        // the developer template was created by an older application version.
        await EnsureAccountingSchemaAsync(masterConn, schemaName);
        await EnsureHrSchemaAsync(masterConn, schemaName);

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
                "GL_OPENING_BALANCE_HEADER",
                $"""
                CREATE TABLE "{schemaName}"."GL_OPENING_BALANCE_HEADER"
                (
                    "ID"             NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "BRANCH_ID"      NUMBER(19) NOT NULL,
                    "FISCAL_YEAR_ID" NUMBER(19) NOT NULL,
                    "AS_OF_DATE"     DATE NOT NULL,
                    "DESCRIPTION"    NVARCHAR2(500) NULL,
                    "STATUS"         NUMBER(1) DEFAULT 1 NOT NULL,
                    "TOTAL_DEBIT"    NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_CREDIT"   NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "OB_VOUCHER_ID"  NUMBER(19) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER"   NVARCHAR2(100) NULL,
                    "UPDATE_DATE"   TIMESTAMP NULL,
                    CONSTRAINT "PK_GL_OB_HEADER" PRIMARY KEY ("ID"),
                    CONSTRAINT "CK_GL_OB_STATUS" CHECK ("STATUS" IN (1, 2))
                )
                """
            ),
            (
                "GL_OPENING_BALANCE_DETAIL",
                $"""
                CREATE TABLE "{schemaName}"."GL_OPENING_BALANCE_DETAIL"
                (
                    "ID"            NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "HEADER_ID"     NUMBER(19) NOT NULL,
                    "LINE_SER"      NUMBER(5) NOT NULL,
                    "ACCOUNT_CODE"  NVARCHAR2(50) NOT NULL,
                    "DEBIT_AMOUNT"  NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CREDIT_AMOUNT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_DEBIT"   NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "LOCAL_CREDIT"  NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CURRENCY_ID"   NUMBER(19) DEFAULT 1 NOT NULL,
                    "EXCHANGE_RATE" NUMBER(14,6) DEFAULT 1 NOT NULL,
                    "DESCRIPTION"   NVARCHAR2(500) NULL,
                    CONSTRAINT "PK_GL_OB_DETAIL" PRIMARY KEY ("ID"),
                    CONSTRAINT "CK_GL_OB_DEBIT_CREDIT" CHECK (NOT ("DEBIT_AMOUNT" > 0 AND "CREDIT_AMOUNT" > 0))
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
            await ExecuteAccountingDdlAsync(connection, "Accounting column/default", statement);
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

            // Opening Balance constraints
            ("UX_GL_OB_BRANCH_YEAR", $"ALTER TABLE \"{schemaName}\".\"GL_OPENING_BALANCE_HEADER\" ADD CONSTRAINT \"UX_GL_OB_BRANCH_YEAR\" UNIQUE (\"BRANCH_ID\", \"FISCAL_YEAR_ID\")"),
            ("FK_GL_OB_DETAIL_HDR", $"ALTER TABLE \"{schemaName}\".\"GL_OPENING_BALANCE_DETAIL\" ADD CONSTRAINT \"FK_GL_OB_DETAIL_HDR\" FOREIGN KEY (\"HEADER_ID\") REFERENCES \"{schemaName}\".\"GL_OPENING_BALANCE_HEADER\" (\"ID\") ON DELETE CASCADE"),
            ("UX_GL_OB_DETAIL_LINE", $"ALTER TABLE \"{schemaName}\".\"GL_OPENING_BALANCE_DETAIL\" ADD CONSTRAINT \"UX_GL_OB_DETAIL_LINE\" UNIQUE (\"HEADER_ID\", \"LINE_SER\")")
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

            // Opening Balance indexes
            ("IX_GL_OB_DETAIL_ACC", $"CREATE INDEX \"{schemaName}\".\"IX_GL_OB_DETAIL_ACC\" ON \"{schemaName}\".\"GL_OPENING_BALANCE_DETAIL\" (\"ACCOUNT_CODE\")")
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

    /// <summary>
    /// Ensures all HR & Payroll tables, sequences, constraints, foreign keys, and statutory rules exist.
    /// </summary>
    private async Task EnsureHrSchemaAsync(OracleConnection connection, string schemaName)
    {
        schemaName = NormalizeOracleIdentifier(schemaName);
        _logger.LogInformation("Ensuring HR & Payroll schema objects in {SchemaName}", schemaName);

        var tableStatements = new (string Name, string Sql)[]
        {
            (
                "HR_DEPARTMENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_DEPARTMENT"
                (
                    "DEPARTMENT_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "PARENT_DEPARTMENT_CODE" NVARCHAR2(50) NULL,
                    "MANAGER_EMPLOYEE_CODE" NVARCHAR2(50) NULL,
                    "COST_CENTER_CODE" NVARCHAR2(50) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_DEPARTMENT" PRIMARY KEY ("DEPARTMENT_CODE")
                )
                """
            ),
            (
                "HR_JOB_GRADE",
                $"""
                CREATE TABLE "{schemaName}"."HR_JOB_GRADE"
                (
                    "GRADE_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "LEVEL_NO" NUMBER(5) DEFAULT 1 NOT NULL,
                    "MIN_SALARY" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "MID_SALARY" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "MAX_SALARY" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_JOB_GRADE" PRIMARY KEY ("GRADE_CODE")
                )
                """
            ),
            (
                "HR_POSITION",
                $"""
                CREATE TABLE "{schemaName}"."HR_POSITION"
                (
                    "POSITION_CODE" NVARCHAR2(50) NOT NULL,
                    "TITLE_AR" NVARCHAR2(200) NOT NULL,
                    "TITLE_EN" NVARCHAR2(200) NOT NULL,
                    "DEPARTMENT_CODE" NVARCHAR2(50) NOT NULL,
                    "JOB_GRADE_CODE" NVARCHAR2(50) NOT NULL,
                    "REPORTS_TO_POSITION_CODE" NVARCHAR2(50) NULL,
                    "HEADCOUNT" NUMBER(5) DEFAULT 1 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_POSITION" PRIMARY KEY ("POSITION_CODE")
                )
                """
            ),
            (
                "HR_EMPLOYEE",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMPLOYEE"
                (
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "NATIONAL_ID" NVARCHAR2(50) NOT NULL,
                    "PASSPORT_NUMBER" NVARCHAR2(50) NULL,
                    "NATIONALITY" NVARCHAR2(100) DEFAULT 'Jordanian' NOT NULL,
                    "DATE_OF_BIRTH" TIMESTAMP NOT NULL,
                    "GENDER" NVARCHAR2(20) DEFAULT 'MALE' NOT NULL,
                    "MARITAL_STATUS" NVARCHAR2(20) DEFAULT 'SINGLE' NOT NULL,
                    "HIRE_DATE" TIMESTAMP NOT NULL,
                    "POSITION_CODE" NVARCHAR2(50) NULL,
                    "DEPARTMENT_CODE" NVARCHAR2(50) NULL,
                    "BRANCH_ID" NUMBER(19) NULL,
                    "EMPLOYMENT_TYPE" NVARCHAR2(50) DEFAULT 'FULL_TIME' NOT NULL,
                    "EMPLOYMENT_STATUS" NVARCHAR2(50) DEFAULT 'ACTIVE' NOT NULL,
                    "SSC_NUMBER" NVARCHAR2(50) NULL,
                    "IS_HIGH_RISK_ROLE" NUMBER(1) DEFAULT 0 NOT NULL,
                    "TAX_EXEMPTION_COUNT" NUMBER(5) DEFAULT 0 NOT NULL,
                    "BANK_ACCOUNT_NUMBER" NVARCHAR2(50) NULL,
                    "BANK_NAME" NVARCHAR2(150) NULL,
                    "BANK_IBAN" NVARCHAR2(50) NULL,
                    "EMAIL" NVARCHAR2(150) NULL,
                    "PHONE" NVARCHAR2(50) NULL,
                    "PROBATION_END_DATE" TIMESTAMP NULL,
                    "TERMINATION_DATE" TIMESTAMP NULL,
                    "TERMINATION_REASON" NVARCHAR2(500) NULL,
                    "MANAGER_EMPLOYEE_CODE" NVARCHAR2(50) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_EMPLOYEE" PRIMARY KEY ("EMPLOYEE_CODE")
                )
                """
            ),
            (
                "HR_EMPLOYEE_DEPENDENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMPLOYEE_DEPENDENT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "RELATIONSHIP" NVARCHAR2(50) DEFAULT 'CHILD' NOT NULL,
                    "NATIONAL_ID" NVARCHAR2(50) NULL,
                    "DATE_OF_BIRTH" TIMESTAMP NOT NULL,
                    "GENDER" NVARCHAR2(20) DEFAULT 'MALE' NOT NULL,
                    "IS_TAX_EXEMPTION_CLAIMED" NUMBER(1) DEFAULT 1 NOT NULL,
                    "IS_MEDICAL_COVERED" NUMBER(1) DEFAULT 0 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_EMP_DEPENDENT" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_EMPLOYEE_DOCUMENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMPLOYEE_DOCUMENT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "DOCUMENT_TYPE" NVARCHAR2(50) DEFAULT 'NATIONAL_ID' NOT NULL,
                    "DOCUMENT_NUMBER" NVARCHAR2(100) NULL,
                    "FILE_REFERENCE" NVARCHAR2(500) NOT NULL,
                    "FILE_NAME" NVARCHAR2(255) NULL,
                    "ISSUED_DATE" TIMESTAMP NULL,
                    "EXPIRY_DATE" TIMESTAMP NULL,
                    "NOTES" NVARCHAR2(500) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_EMP_DOCUMENT" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_EMPLOYMENT_EVENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMPLOYMENT_EVENT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "EVENT_TYPE" NVARCHAR2(50) DEFAULT 'HIRE' NOT NULL,
                    "EFFECTIVE_DATE" TIMESTAMP NOT NULL,
                    "FROM_VALUE" NVARCHAR2(255) NULL,
                    "TO_VALUE" NVARCHAR2(255) NULL,
                    "REASON" NVARCHAR2(500) NULL,
                    "APPROVED_BY" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_EMP_EVENT" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_SHIFT_SCHEDULE",
                $"""
                CREATE TABLE "{schemaName}"."HR_SHIFT_SCHEDULE"
                (
                    "SHIFT_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "START_TIME" INTERVAL DAY TO SECOND NOT NULL,
                    "END_TIME" INTERVAL DAY TO SECOND NOT NULL,
                    "BREAK_MINUTES" NUMBER(5) DEFAULT 60 NOT NULL,
                    "WORKING_DAYS_JSON" NVARCHAR2(200) DEFAULT '["Sun","Mon","Tue","Wed","Thu"]' NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_SHIFT_SCHEDULE" PRIMARY KEY ("SHIFT_CODE")
                )
                """
            ),
            (
                "HR_EMP_SHIFT_ASSIGNMENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMP_SHIFT_ASSIGNMENT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "SHIFT_CODE" NVARCHAR2(50) NOT NULL,
                    "EFFECTIVE_FROM" TIMESTAMP NOT NULL,
                    "EFFECTIVE_TO" TIMESTAMP NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_EMP_SHIFT_ASGN" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_ATTENDANCE_RECORD",
                $"""
                CREATE TABLE "{schemaName}"."HR_ATTENDANCE_RECORD"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "ATTENDANCE_DATE" TIMESTAMP NOT NULL,
                    "CLOCK_IN" TIMESTAMP NULL,
                    "CLOCK_OUT" TIMESTAMP NULL,
                    "SOURCE" NVARCHAR2(50) DEFAULT 'WEB' NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'ON_TIME' NOT NULL,
                    "LATE_MINUTES" NUMBER(10) DEFAULT 0 NOT NULL,
                    "EARLY_LEAVE_MINUTES" NUMBER(10) DEFAULT 0 NOT NULL,
                    "TOTAL_WORK_HOURS" NUMBER(10,2) DEFAULT 0 NOT NULL,
                    "CORRECTED_BY" NVARCHAR2(100) NULL,
                    "CORRECTION_REASON" NVARCHAR2(500) NULL,
                    "IDEMPOTENCY_KEY" NVARCHAR2(100) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_ATTENDANCE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_OVERTIME_RECORD",
                $"""
                CREATE TABLE "{schemaName}"."HR_OVERTIME_RECORD"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "OVERTIME_DATE" TIMESTAMP NOT NULL,
                    "HOURS" NUMBER(10,2) NOT NULL,
                    "RATE_MULTIPLIER" NUMBER(5,2) DEFAULT 1.25 NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'PENDING' NOT NULL,
                    "REASON" NVARCHAR2(500) NULL,
                    "APPROVED_BY" NVARCHAR2(100) NULL,
                    "APPROVAL_DATE" TIMESTAMP NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_OVERTIME" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_LEAVE_TYPE",
                $"""
                CREATE TABLE "{schemaName}"."HR_LEAVE_TYPE"
                (
                    "LEAVE_TYPE_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "IS_PAID" NUMBER(1) DEFAULT 1 NOT NULL,
                    "IS_STATUTORY" NUMBER(1) DEFAULT 1 NOT NULL,
                    "REQUIRES_DOCUMENTATION" NUMBER(1) DEFAULT 0 NOT NULL,
                    "MAX_DAYS_PER_YEAR" NUMBER(5,2) DEFAULT 14 NOT NULL,
                    "CARRY_FORWARD_ALLOWED" NUMBER(1) DEFAULT 0 NOT NULL,
                    "CARRY_FORWARD_CAP_DAYS" NUMBER(5,2) DEFAULT 0 NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_LEAVE_TYPE" PRIMARY KEY ("LEAVE_TYPE_CODE")
                )
                """
            ),
            (
                "HR_LEAVE_POLICY",
                $"""
                CREATE TABLE "{schemaName}"."HR_LEAVE_POLICY"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "LEAVE_TYPE_CODE" NVARCHAR2(50) NOT NULL,
                    "MIN_SERVICE_MONTHS" NUMBER(5) DEFAULT 0 NOT NULL,
                    "MAX_SERVICE_MONTHS" NUMBER(5) NULL,
                    "ANNUAL_ENTITLEMENT_DAYS" NUMBER(5,2) NOT NULL,
                    "MONTHLY_ACCRUAL_RATE" NUMBER(5,4) NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_LEAVE_POLICY" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_LEAVE_BALANCE",
                $"""
                CREATE TABLE "{schemaName}"."HR_LEAVE_BALANCE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "LEAVE_TYPE_CODE" NVARCHAR2(50) NOT NULL,
                    "YEAR_NO" NUMBER(4) NOT NULL,
                    "ACCRUED_DAYS" NUMBER(6,2) DEFAULT 0 NOT NULL,
                    "USED_DAYS" NUMBER(6,2) DEFAULT 0 NOT NULL,
                    "CARRIED_FORWARD_DAYS" NUMBER(6,2) DEFAULT 0 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_LEAVE_BALANCE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_LEAVE_REQUEST",
                $"""
                CREATE TABLE "{schemaName}"."HR_LEAVE_REQUEST"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "LEAVE_TYPE_CODE" NVARCHAR2(50) NOT NULL,
                    "START_DATE" TIMESTAMP NOT NULL,
                    "END_DATE" TIMESTAMP NOT NULL,
                    "TOTAL_DAYS" NUMBER(6,2) NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'SUBMITTED' NOT NULL,
                    "REASON" NVARCHAR2(500) NULL,
                    "ATTACHMENT_REF" NVARCHAR2(500) NULL,
                    "APPROVED_BY" NVARCHAR2(100) NULL,
                    "APPROVAL_DATE" TIMESTAMP NULL,
                    "REJECTION_REASON" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_LEAVE_REQUEST" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_SALARY_COMPONENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_SALARY_COMPONENT"
                (
                    "COMPONENT_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "COMPONENT_TYPE" NVARCHAR2(50) DEFAULT 'EARNING' NOT NULL,
                    "IS_TAXABLE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "IS_SSC_APPLICABLE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CALCULATION_TYPE" NVARCHAR2(50) DEFAULT 'FIXED_AMOUNT' NOT NULL,
                    "DEFAULT_AMOUNT" NUMBER(18,4) NULL,
                    "DEFAULT_PERCENT" NUMBER(8,4) NULL,
                    "GL_ACCOUNT_CODE" NVARCHAR2(50) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_SAL_COMPONENT" PRIMARY KEY ("COMPONENT_CODE")
                )
                """
            ),
            (
                "HR_EMP_SALARY_STRUCTURE",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMP_SALARY_STRUCTURE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "EFFECTIVE_FROM" TIMESTAMP NOT NULL,
                    "EFFECTIVE_TO" TIMESTAMP NULL,
                    "BASIC_SALARY" NUMBER(18,3) NOT NULL,
                    "CURRENCY_CODE" NVARCHAR2(10) DEFAULT 'JOD' NOT NULL,
                    "PAYMENT_METHOD" NVARCHAR2(50) DEFAULT 'BANK_TRANSFER' NOT NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_SAL_STRUCTURE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_EMP_SALARY_STRUCT_LINE",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMP_SALARY_STRUCT_LINE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "STRUCTURE_ID" NUMBER(19) NOT NULL,
                    "COMPONENT_CODE" NVARCHAR2(50) NOT NULL,
                    "AMOUNT" NUMBER(18,3) NOT NULL,
                    "PERCENT_VALUE" NUMBER(8,4) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_SAL_STR_LINE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_EMPLOYMENT_CONTRACT",
                $"""
                CREATE TABLE "{schemaName}"."HR_EMPLOYMENT_CONTRACT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "CONTRACT_TYPE" NVARCHAR2(50) DEFAULT 'UNLIMITED' NOT NULL,
                    "START_DATE" TIMESTAMP NOT NULL,
                    "END_DATE" TIMESTAMP NULL,
                    "PROBATION_MONTHS" NUMBER(5) DEFAULT 3 NOT NULL,
                    "NOTICE_PERIOD_DAYS" NUMBER(5) DEFAULT 30 NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'ACTIVE' NOT NULL,
                    "FILE_REFERENCE" NVARCHAR2(500) NULL,
                    "NOTES" NVARCHAR2(500) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_EMP_CONTRACT" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_JOB_REQUISITION",
                $"""
                CREATE TABLE "{schemaName}"."HR_JOB_REQUISITION"
                (
                    "REQUISITION_CODE" NVARCHAR2(50) NOT NULL,
                    "POSITION_CODE" NVARCHAR2(50) NOT NULL,
                    "DEPARTMENT_CODE" NVARCHAR2(50) NOT NULL,
                    "BRANCH_ID" NUMBER(19) NULL,
                    "HEADCOUNT" NUMBER(5) DEFAULT 1 NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'OPEN' NOT NULL,
                    "REQUESTED_BY" NVARCHAR2(50) NOT NULL,
                    "APPROVED_BY" NVARCHAR2(100) NULL,
                    "APPROVAL_DATE" TIMESTAMP NULL,
                    "JOB_DESCRIPTION" NVARCHAR2(2000) NULL,
                    "REQUIREMENTS" NVARCHAR2(2000) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_JOB_REQUISITION" PRIMARY KEY ("REQUISITION_CODE")
                )
                """
            ),
            (
                "HR_CANDIDATE",
                $"""
                CREATE TABLE "{schemaName}"."HR_CANDIDATE"
                (
                    "CANDIDATE_CODE" NVARCHAR2(50) NOT NULL,
                    "NAME_AR" NVARCHAR2(200) NOT NULL,
                    "NAME_EN" NVARCHAR2(200) NOT NULL,
                    "EMAIL" NVARCHAR2(150) NOT NULL,
                    "PHONE" NVARCHAR2(50) NULL,
                    "NATIONAL_ID" NVARCHAR2(50) NULL,
                    "RESUME_FILE_REF" NVARCHAR2(500) NULL,
                    "SOURCE" NVARCHAR2(50) DEFAULT 'DIRECT' NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_CANDIDATE" PRIMARY KEY ("CANDIDATE_CODE")
                )
                """
            ),
            (
                "HR_CANDIDATE_APPLICATION",
                $"""
                CREATE TABLE "{schemaName}"."HR_CANDIDATE_APPLICATION"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "CANDIDATE_CODE" NVARCHAR2(50) NOT NULL,
                    "REQUISITION_CODE" NVARCHAR2(50) NOT NULL,
                    "STAGE" NVARCHAR2(50) DEFAULT 'APPLIED' NOT NULL,
                    "OFFERED_SALARY" NUMBER(18,3) NULL,
                    "INTERVIEW_DATE" TIMESTAMP NULL,
                    "NOTES" NVARCHAR2(1000) NULL,
                    "HIRED_EMPLOYEE_CODE" NVARCHAR2(50) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_CAND_APP" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_ONBOARDING_TASK",
                $"""
                CREATE TABLE "{schemaName}"."HR_ONBOARDING_TASK"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "TASK_NAME" NVARCHAR2(200) NOT NULL,
                    "ASSIGNED_TO" NVARCHAR2(100) NULL,
                    "DUE_DATE" TIMESTAMP NULL,
                    "IS_COMPLETED" NUMBER(1) DEFAULT 0 NOT NULL,
                    "COMPLETED_DATE" TIMESTAMP NULL,
                    "COMPLETED_BY" NVARCHAR2(100) NULL,
                    "NOTES" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_ONBOARDING_TASK" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_PAYROLL_RUN",
                $"""
                CREATE TABLE "{schemaName}"."HR_PAYROLL_RUN"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "PAY_PERIOD" NVARCHAR2(7) NOT NULL,
                    "RUN_DATE" TIMESTAMP NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'DRAFT' NOT NULL,
                    "TOTAL_GROSS_SALARY" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_NET_SALARY" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_EMPLOYEE_SSC" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_EMPLOYER_SSC" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_INCOME_TAX" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_NATIONAL_CONTRIB" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_OTHER_DEDUCTIONS" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "BRANCH_ID" NUMBER(19) NULL,
                    "JOURNAL_VOUCHER_ID" NUMBER(19) NULL,
                    "CALCULATED_BY" NVARCHAR2(100) NOT NULL,
                    "CALCULATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "APPROVED_BY" NVARCHAR2(100) NULL,
                    "APPROVAL_DATE" TIMESTAMP NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_PAYROLL_RUN" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_PAYROLL_RUN_LINE",
                $"""
                CREATE TABLE "{schemaName}"."HR_PAYROLL_RUN_LINE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "PAYROLL_RUN_ID" NUMBER(19) NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "BASIC_SALARY" NUMBER(18,3) NOT NULL,
                    "TOTAL_ALLOWANCES" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "TOTAL_DEDUCTIONS" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "GROSS_SALARY" NUMBER(18,3) NOT NULL,
                    "EMPLOYEE_SSC" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "EMPLOYER_SSC" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "INCOME_TAX" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "NET_SALARY" NUMBER(18,3) NOT NULL,
                    "PAYMENT_STATUS" NVARCHAR2(50) DEFAULT 'UNPAID' NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_PAY_RUN_LINE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_PAYROLL_RUN_LINE_COMPONENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_PAYROLL_RUN_LINE_COMPONENT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "PAYROLL_LINE_ID" NUMBER(19) NOT NULL,
                    "COMPONENT_CODE" NVARCHAR2(50) NOT NULL,
                    "AMOUNT" NUMBER(18,3) NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_PAY_LINE_COMP" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_EOS_PROVISION_ACCRUAL",
                $"""
                CREATE TABLE "{schemaName}"."HR_EOS_PROVISION_ACCRUAL"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "YEAR_NO" NUMBER(4) NOT NULL,
                    "MONTH_NO" NUMBER(2) NOT NULL,
                    "MONTHLY_PROVISION_AMOUNT" NUMBER(18,3) NOT NULL,
                    "CUMULATIVE_PROVISION_AMOUNT" NUMBER(18,3) NOT NULL,
                    "JOURNAL_VOUCHER_ID" NUMBER(19) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_EOS_PROVISION" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_EOS_SETTLEMENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_EOS_SETTLEMENT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "SETTLEMENT_DATE" TIMESTAMP NOT NULL,
                    "TERMINATION_TYPE" NVARCHAR2(50) DEFAULT 'RESIGNATION' NOT NULL,
                    "TOTAL_SERVICE_YEARS" NUMBER(5,2) NOT NULL,
                    "GRATUITY_AMOUNT" NUMBER(18,3) NOT NULL,
                    "LEAVE_ENCASHMENT_AMOUNT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "DEDUCTIONS_AMOUNT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "NET_SETTLEMENT_AMOUNT" NUMBER(18,3) NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'DRAFT' NOT NULL,
                    "APPROVED_BY" NVARCHAR2(100) NULL,
                    "APPROVAL_DATE" TIMESTAMP NULL,
                    "JOURNAL_VOUCHER_ID" NUMBER(19) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_EOS_SETTLE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_EXPENSE_CLAIM",
                $"""
                CREATE TABLE "{schemaName}"."HR_EXPENSE_CLAIM"
                (
                    "CLAIM_NUMBER" NVARCHAR2(50) NOT NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "CLAIM_DATE" TIMESTAMP NOT NULL,
                    "TOTAL_AMOUNT" NUMBER(18,3) DEFAULT 0 NOT NULL,
                    "CURRENCY_CODE" NVARCHAR2(10) DEFAULT 'JOD' NOT NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'SUBMITTED' NOT NULL,
                    "PURPOSE" NVARCHAR2(500) NULL,
                    "APPROVED_BY" NVARCHAR2(100) NULL,
                    "APPROVAL_DATE" TIMESTAMP NULL,
                    "JOURNAL_VOUCHER_ID" NUMBER(19) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_EXPENSE_CLAIM" PRIMARY KEY ("CLAIM_NUMBER")
                )
                """
            ),
            (
                "HR_EXPENSE_CLAIM_LINE",
                $"""
                CREATE TABLE "{schemaName}"."HR_EXPENSE_CLAIM_LINE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "CLAIM_NUMBER" NVARCHAR2(50) NOT NULL,
                    "EXPENSE_TYPE" NVARCHAR2(50) NOT NULL,
                    "EXPENSE_DATE" TIMESTAMP NOT NULL,
                    "AMOUNT" NUMBER(18,3) NOT NULL,
                    "RECEIPT_FILE_REF" NVARCHAR2(500) NULL,
                    "DESCRIPTION" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    CONSTRAINT "PK_HR_EXPENSE_LINE" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_ASSET_ASSIGNMENT",
                $"""
                CREATE TABLE "{schemaName}"."HR_ASSET_ASSIGNMENT"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "ASSET_TAG" NVARCHAR2(50) NOT NULL,
                    "ASSET_DESCRIPTION" NVARCHAR2(200) NOT NULL,
                    "CATEGORY" NVARCHAR2(50) DEFAULT 'LAPTOP' NOT NULL,
                    "SERIAL_NUMBER" NVARCHAR2(100) NULL,
                    "EMPLOYEE_CODE" NVARCHAR2(50) NOT NULL,
                    "ISSUED_DATE" TIMESTAMP NOT NULL,
                    "RETURNED_DATE" TIMESTAMP NULL,
                    "STATUS" NVARCHAR2(50) DEFAULT 'ASSIGNED' NOT NULL,
                    "CONDITION_NOTES" NVARCHAR2(500) NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_ASSET_ASSIGNMENT" PRIMARY KEY ("ID")
                )
                """
            ),
            (
                "HR_STATUTORY_RULE",
                $"""
                CREATE TABLE "{schemaName}"."HR_STATUTORY_RULE"
                (
                    "ID" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                    "RULE_TYPE" NVARCHAR2(50) NOT NULL,
                    "RULE_NAME" NVARCHAR2(200) NOT NULL,
                    "BRACKET_LOW" NUMBER(18,3) NULL,
                    "BRACKET_HIGH" NUMBER(18,3) NULL,
                    "RATE_PERCENT" NUMBER(8,4) NULL,
                    "VALUE" NUMBER(18,3) NULL,
                    "CURRENCY_CODE" NVARCHAR2(10) DEFAULT 'JOD' NOT NULL,
                    "EFFECTIVE_FROM" TIMESTAMP NOT NULL,
                    "EFFECTIVE_TO" TIMESTAMP NULL,
                    "DESCRIPTION" NVARCHAR2(500) NULL,
                    "IS_ACTIVE" NUMBER(1) DEFAULT 1 NOT NULL,
                    "CREATION_USER" NVARCHAR2(100) NOT NULL,
                    "CREATION_DATE" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                    "UPDATE_USER" NVARCHAR2(100) NULL,
                    "UPDATE_DATE" TIMESTAMP NULL,
                    CONSTRAINT "PK_HR_STAT_RULE" PRIMARY KEY ("ID")
                )
                """
            )
        };

        foreach (var statement in tableStatements)
        {
            await ExecuteAccountingDdlAsync(connection, $"HR table {statement.Name}", statement.Sql);
        }

        var constraintStatements = new (string Name, string Sql)[]
        {
            ("FK_HRPOS_DEPT", $"ALTER TABLE \"{schemaName}\".\"HR_POSITION\" ADD CONSTRAINT \"FK_HRPOS_DEPT\" FOREIGN KEY (\"DEPARTMENT_CODE\") REFERENCES \"{schemaName}\".\"HR_DEPARTMENT\" (\"DEPARTMENT_CODE\")"),
            ("FK_HRPOS_GRADE", $"ALTER TABLE \"{schemaName}\".\"HR_POSITION\" ADD CONSTRAINT \"FK_HRPOS_GRADE\" FOREIGN KEY (\"JOB_GRADE_CODE\") REFERENCES \"{schemaName}\".\"HR_JOB_GRADE\" (\"GRADE_CODE\")"),
            ("FK_HREMP_DEPT", $"ALTER TABLE \"{schemaName}\".\"HR_EMPLOYEE\" ADD CONSTRAINT \"FK_HREMP_DEPT\" FOREIGN KEY (\"DEPARTMENT_CODE\") REFERENCES \"{schemaName}\".\"HR_DEPARTMENT\" (\"DEPARTMENT_CODE\")"),
            ("FK_HREMP_POS", $"ALTER TABLE \"{schemaName}\".\"HR_EMPLOYEE\" ADD CONSTRAINT \"FK_HREMP_POS\" FOREIGN KEY (\"POSITION_CODE\") REFERENCES \"{schemaName}\".\"HR_POSITION\" (\"POSITION_CODE\")"),
            ("FK_HREMPDEP_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EMPLOYEE_DEPENDENT\" ADD CONSTRAINT \"FK_HREMPDEP_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HREMPDOC_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EMPLOYEE_DOCUMENT\" ADD CONSTRAINT \"FK_HREMPDOC_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HREMPEVT_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EMPLOYMENT_EVENT\" ADD CONSTRAINT \"FK_HREMPEVT_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HREMPSHIFT_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EMP_SHIFT_ASSIGNMENT\" ADD CONSTRAINT \"FK_HREMPSHIFT_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HREMPSHIFT_SHIFT", $"ALTER TABLE \"{schemaName}\".\"HR_EMP_SHIFT_ASSIGNMENT\" ADD CONSTRAINT \"FK_HREMPSHIFT_SHIFT\" FOREIGN KEY (\"SHIFT_CODE\") REFERENCES \"{schemaName}\".\"HR_SHIFT_SCHEDULE\" (\"SHIFT_CODE\")"),
            ("FK_HRATT_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_ATTENDANCE_RECORD\" ADD CONSTRAINT \"FK_HRATT_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HROT_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_OVERTIME_RECORD\" ADD CONSTRAINT \"FK_HROT_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HRLVPOL_TYPE", $"ALTER TABLE \"{schemaName}\".\"HR_LEAVE_POLICY\" ADD CONSTRAINT \"FK_HRLVPOL_TYPE\" FOREIGN KEY (\"LEAVE_TYPE_CODE\") REFERENCES \"{schemaName}\".\"HR_LEAVE_TYPE\" (\"LEAVE_TYPE_CODE\")"),
            ("FK_HRLVBAL_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_LEAVE_BALANCE\" ADD CONSTRAINT \"FK_HRLVBAL_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HRLVBAL_TYPE", $"ALTER TABLE \"{schemaName}\".\"HR_LEAVE_BALANCE\" ADD CONSTRAINT \"FK_HRLVBAL_TYPE\" FOREIGN KEY (\"LEAVE_TYPE_CODE\") REFERENCES \"{schemaName}\".\"HR_LEAVE_TYPE\" (\"LEAVE_TYPE_CODE\")"),
            ("UX_HR_LEAVE_BAL", $"ALTER TABLE \"{schemaName}\".\"HR_LEAVE_BALANCE\" ADD CONSTRAINT \"UX_HR_LEAVE_BAL\" UNIQUE (\"EMPLOYEE_CODE\", \"LEAVE_TYPE_CODE\", \"YEAR_NO\")"),
            ("FK_HRLVREQ_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_LEAVE_REQUEST\" ADD CONSTRAINT \"FK_HRLVREQ_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HRLVREQ_TYPE", $"ALTER TABLE \"{schemaName}\".\"HR_LEAVE_REQUEST\" ADD CONSTRAINT \"FK_HRLVREQ_TYPE\" FOREIGN KEY (\"LEAVE_TYPE_CODE\") REFERENCES \"{schemaName}\".\"HR_LEAVE_TYPE\" (\"LEAVE_TYPE_CODE\")"),
            ("FK_HRSALSTR_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EMP_SALARY_STRUCTURE\" ADD CONSTRAINT \"FK_HRSALSTR_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HRSALSTRLN_STR", $"ALTER TABLE \"{schemaName}\".\"HR_EMP_SALARY_STRUCT_LINE\" ADD CONSTRAINT \"FK_HRSALSTRLN_STR\" FOREIGN KEY (\"STRUCTURE_ID\") REFERENCES \"{schemaName}\".\"HR_EMP_SALARY_STRUCTURE\" (\"ID\") ON DELETE CASCADE"),
            ("FK_HRSALSTRLN_COMP", $"ALTER TABLE \"{schemaName}\".\"HR_EMP_SALARY_STRUCT_LINE\" ADD CONSTRAINT \"FK_HRSALSTRLN_COMP\" FOREIGN KEY (\"COMPONENT_CODE\") REFERENCES \"{schemaName}\".\"HR_SALARY_COMPONENT\" (\"COMPONENT_CODE\")"),
            ("FK_HREMPCTR_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EMPLOYMENT_CONTRACT\" ADD CONSTRAINT \"FK_HREMPCTR_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HRREQ_POS", $"ALTER TABLE \"{schemaName}\".\"HR_JOB_REQUISITION\" ADD CONSTRAINT \"FK_HRREQ_POS\" FOREIGN KEY (\"POSITION_CODE\") REFERENCES \"{schemaName}\".\"HR_POSITION\" (\"POSITION_CODE\")"),
            ("FK_HRREQ_DEPT", $"ALTER TABLE \"{schemaName}\".\"HR_JOB_REQUISITION\" ADD CONSTRAINT \"FK_HRREQ_DEPT\" FOREIGN KEY (\"DEPARTMENT_CODE\") REFERENCES \"{schemaName}\".\"HR_DEPARTMENT\" (\"DEPARTMENT_CODE\")"),
            ("FK_HRCANDAPP_CAND", $"ALTER TABLE \"{schemaName}\".\"HR_CANDIDATE_APPLICATION\" ADD CONSTRAINT \"FK_HRCANDAPP_CAND\" FOREIGN KEY (\"CANDIDATE_CODE\") REFERENCES \"{schemaName}\".\"HR_CANDIDATE\" (\"CANDIDATE_CODE\") ON DELETE CASCADE"),
            ("FK_HRCANDAPP_REQ", $"ALTER TABLE \"{schemaName}\".\"HR_CANDIDATE_APPLICATION\" ADD CONSTRAINT \"FK_HRCANDAPP_REQ\" FOREIGN KEY (\"REQUISITION_CODE\") REFERENCES \"{schemaName}\".\"HR_JOB_REQUISITION\" (\"REQUISITION_CODE\") ON DELETE CASCADE"),
            ("FK_HRONBTASK_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_ONBOARDING_TASK\" ADD CONSTRAINT \"FK_HRONBTASK_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HRPAYLN_RUN", $"ALTER TABLE \"{schemaName}\".\"HR_PAYROLL_RUN_LINE\" ADD CONSTRAINT \"FK_HRPAYLN_RUN\" FOREIGN KEY (\"PAYROLL_RUN_ID\") REFERENCES \"{schemaName}\".\"HR_PAYROLL_RUN\" (\"ID\") ON DELETE CASCADE"),
            ("FK_HRPAYLN_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_PAYROLL_RUN_LINE\" ADD CONSTRAINT \"FK_HRPAYLN_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\")"),
            ("FK_HRPAYLNCOMP_LN", $"ALTER TABLE \"{schemaName}\".\"HR_PAYROLL_RUN_LINE_COMPONENT\" ADD CONSTRAINT \"FK_HRPAYLNCOMP_LN\" FOREIGN KEY (\"PAYROLL_LINE_ID\") REFERENCES \"{schemaName}\".\"HR_PAYROLL_RUN_LINE\" (\"ID\") ON DELETE CASCADE"),
            ("FK_HREOSPROV_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EOS_PROVISION_ACCRUAL\" ADD CONSTRAINT \"FK_HREOSPROV_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\") ON DELETE CASCADE"),
            ("FK_HREOSSETTLE_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EOS_SETTLEMENT\" ADD CONSTRAINT \"FK_HREOSSETTLE_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\")"),
            ("FK_HREXP_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_EXPENSE_CLAIM\" ADD CONSTRAINT \"FK_HREXP_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\")"),
            ("FK_HREXP_LN_CLAIM", $"ALTER TABLE \"{schemaName}\".\"HR_EXPENSE_CLAIM_LINE\" ADD CONSTRAINT \"FK_HREXP_LN_CLAIM\" FOREIGN KEY (\"CLAIM_NUMBER\") REFERENCES \"{schemaName}\".\"HR_EXPENSE_CLAIM\" (\"CLAIM_NUMBER\") ON DELETE CASCADE"),
            ("FK_HRASSET_EMP", $"ALTER TABLE \"{schemaName}\".\"HR_ASSET_ASSIGNMENT\" ADD CONSTRAINT \"FK_HRASSET_EMP\" FOREIGN KEY (\"EMPLOYEE_CODE\") REFERENCES \"{schemaName}\".\"HR_EMPLOYEE\" (\"EMPLOYEE_CODE\")")
        };

        foreach (var constraint in constraintStatements)
        {
            await ExecuteAccountingDdlAsync(connection, $"HR constraint {constraint.Name}", constraint.Sql);
        }

        await SeedHrDefaultDataAsync(connection, schemaName);
    }

    private async Task SeedHrDefaultDataAsync(OracleConnection connection, string schemaName)
    {
        _logger.LogInformation("Seeding default HR master and test data in {SchemaName}", schemaName);

        var seedStatements = new[]
        {
            // 1. Statutory Rules
            $"""
            MERGE INTO "{schemaName}"."HR_STATUTORY_RULE" target
            USING (
                SELECT 'SSC_EMPLOYEE_RATE' AS R_TYPE, 'Jordan SSC Employee Contribution Rate' AS R_NAME, 0.0750 AS RATE, CAST(NULL AS NUMBER(18,3)) AS VAL, CAST(NULL AS NUMBER(18,3)) AS B_LOW, CAST(NULL AS NUMBER(18,3)) AS B_HIGH FROM DUAL UNION ALL
                SELECT 'SSC_EMPLOYER_RATE', 'Jordan SSC Employer Contribution Rate', 0.1425, NULL, NULL, NULL FROM DUAL UNION ALL
                SELECT 'SSC_HIGH_RISK_SURCHARGE', 'Jordan SSC High Risk Surcharge', 0.0100, NULL, NULL, NULL FROM DUAL UNION ALL
                SELECT 'SSC_MAX_CEILING', 'Jordan SSC Monthly Wage Cap', NULL, 3349.00, NULL, NULL FROM DUAL UNION ALL
                SELECT 'MINIMUM_WAGE', 'Jordan Minimum Monthly Wage', NULL, 290.00, NULL, NULL FROM DUAL UNION ALL
                SELECT 'PERSONAL_EXEMPTION_SELF', 'Jordan Personal Exemption', NULL, 9000.00, NULL, NULL FROM DUAL UNION ALL
                SELECT 'PERSONAL_EXEMPTION_DEPENDENT', 'Jordan Dependent Exemption', NULL, 1000.00, NULL, NULL FROM DUAL UNION ALL
                SELECT 'ISTD_TAX_BRACKET_1', 'ISTD Tax Bracket 1 (0-5k)', 0.0500, NULL, 0, 5000 FROM DUAL UNION ALL
                SELECT 'ISTD_TAX_BRACKET_2', 'ISTD Tax Bracket 2 (5k-10k)', 0.1000, NULL, 5000, 10000 FROM DUAL UNION ALL
                SELECT 'ISTD_TAX_BRACKET_3', 'ISTD Tax Bracket 3 (10k-15k)', 0.1500, NULL, 10000, 15000 FROM DUAL UNION ALL
                SELECT 'ISTD_TAX_BRACKET_4', 'ISTD Tax Bracket 4 (15k-20k)', 0.2000, NULL, 15000, 20000 FROM DUAL UNION ALL
                SELECT 'ISTD_TAX_BRACKET_5', 'ISTD Tax Bracket 5 (20k-1M)', 0.2500, NULL, 20000, 1000000 FROM DUAL UNION ALL
                SELECT 'ISTD_TAX_BRACKET_6', 'ISTD Tax Bracket 6 (>1M)', 0.3000, NULL, 1000000, NULL FROM DUAL UNION ALL
                SELECT 'OVERTIME_REGULAR_RATE', 'Overtime Regular Rate 1.25x', NULL, 1.25, NULL, NULL FROM DUAL UNION ALL
                SELECT 'OVERTIME_HOLIDAY_RATE', 'Overtime Holiday Rate 1.50x', NULL, 1.50, NULL, NULL FROM DUAL
            ) src
            ON (target."RULE_TYPE" = src.R_TYPE)
            WHEN MATCHED THEN
                UPDATE SET target."RULE_NAME" = src.R_NAME, target."RATE_PERCENT" = src.RATE, target."VALUE" = src.VAL
            WHEN NOT MATCHED THEN
                INSERT ("RULE_TYPE", "RULE_NAME", "BRACKET_LOW", "BRACKET_HIGH", "RATE_PERCENT", "VALUE", "CURRENCY_CODE", "EFFECTIVE_FROM", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.R_TYPE, src.R_NAME, src.B_LOW, src.B_HIGH, src.RATE, src.VAL, 'JOD', CURRENT_TIMESTAMP, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 2. Departments
            $"""
            MERGE INTO "{schemaName}"."HR_DEPARTMENT" target
            USING (
                SELECT 'HR' AS CODE, 'الموارد البشرية' AS NAME_AR, 'Human Resources' AS NAME_EN, 'CC-101' AS CC FROM DUAL UNION ALL
                SELECT 'ENG', 'الهندسة وتطوير البرمجيات', 'Engineering & Software Development', 'CC-102' FROM DUAL UNION ALL
                SELECT 'FIN', 'المالية والمحاسبة', 'Finance & Accounting', 'CC-103' FROM DUAL UNION ALL
                SELECT 'OPS', 'العمليات واللوجستيات', 'Operations & Logistics', 'CC-104' FROM DUAL
            ) src
            ON (target."DEPARTMENT_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."NAME_AR" = src.NAME_AR, target."NAME_EN" = src.NAME_EN, target."COST_CENTER_CODE" = src.CC
            WHEN NOT MATCHED THEN
                INSERT ("DEPARTMENT_CODE", "NAME_AR", "NAME_EN", "COST_CENTER_CODE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.NAME_AR, src.NAME_EN, src.CC, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 3. Job Grades
            $"""
            MERGE INTO "{schemaName}"."HR_JOB_GRADE" target
            USING (
                SELECT 'EXEC' AS CODE, 'الإدارة التنفيذية' AS NAME_AR, 'Executive Management' AS NAME_EN, 1 AS LVL, 3000.00 AS MIN_S, 5500.00 AS MID_S, 8000.00 AS MAX_S FROM DUAL UNION ALL
                SELECT 'SENIOR', 'المستوى الأول (متقدم)', 'Senior Level', 2, 1500.00, 2350.00, 3200.00 FROM DUAL UNION ALL
                SELECT 'MID', 'المستوى المتوسط', 'Mid Level', 3, 800.00, 1200.00, 1600.00 FROM DUAL UNION ALL
                SELECT 'ENTRY', 'المستوى المبتدئ', 'Entry Level', 4, 350.00, 550.00, 750.00 FROM DUAL
            ) src
            ON (target."GRADE_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."NAME_AR" = src.NAME_AR, target."NAME_EN" = src.NAME_EN, target."MIN_SALARY" = src.MIN_S, target."MID_SALARY" = src.MID_S, target."MAX_SALARY" = src.MAX_S
            WHEN NOT MATCHED THEN
                INSERT ("GRADE_CODE", "NAME_AR", "NAME_EN", "LEVEL_NO", "MIN_SALARY", "MID_SALARY", "MAX_SALARY", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.NAME_AR, src.NAME_EN, src.LVL, src.MIN_S, src.MID_S, src.MAX_S, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 4. Positions
            $"""
            MERGE INTO "{schemaName}"."HR_POSITION" target
            USING (
                SELECT 'HR_DIR' AS CODE, 'مدير الموارد البشرية' AS TITLE_AR, 'HR Director' AS TITLE_EN, 'HR' AS DEPT, 'EXEC' AS GRADE, 1 AS HC FROM DUAL UNION ALL
                SELECT 'SR_SWE', 'مهندس برمجيات أول', 'Senior Software Engineer', 'ENG', 'SENIOR', 3 FROM DUAL UNION ALL
                SELECT 'JR_SWE', 'مهندس برمجيات مبتدئ', 'Junior Software Engineer', 'ENG', 'ENTRY', 2 FROM DUAL UNION ALL
                SELECT 'FIN_ACC', 'محاسب عام أول', 'Senior General Accountant', 'FIN', 'SENIOR', 2 FROM DUAL UNION ALL
                SELECT 'OPS_SUP', 'مشرف عمليات', 'Operations Supervisor', 'OPS', 'MID', 1 FROM DUAL
            ) src
            ON (target."POSITION_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."TITLE_AR" = src.TITLE_AR, target."TITLE_EN" = src.TITLE_EN, target."DEPARTMENT_CODE" = src.DEPT, target."JOB_GRADE_CODE" = src.GRADE, target."HEADCOUNT" = src.HC
            WHEN NOT MATCHED THEN
                INSERT ("POSITION_CODE", "TITLE_AR", "TITLE_EN", "DEPARTMENT_CODE", "JOB_GRADE_CODE", "HEADCOUNT", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.TITLE_AR, src.TITLE_EN, src.DEPT, src.GRADE, src.HC, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 5. Shift Schedule
            $"""
            MERGE INTO "{schemaName}"."HR_SHIFT_SCHEDULE" target
            USING (
                SELECT 'GENERAL' AS CODE, 'دوام رسمي عام' AS NAME_AR, 'General Standard Shift' AS NAME_EN, NUMTODSINTERVAL(8.5, 'HOUR') AS ST, NUMTODSINTERVAL(17, 'HOUR') AS ET, 60 AS BM, '["Sun","Mon","Tue","Wed","Thu"]' AS W_DAYS FROM DUAL
            ) src
            ON (target."SHIFT_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."NAME_AR" = src.NAME_AR, target."NAME_EN" = src.NAME_EN, target."START_TIME" = src.ST, target."END_TIME" = src.ET
            WHEN NOT MATCHED THEN
                INSERT ("SHIFT_CODE", "NAME_AR", "NAME_EN", "START_TIME", "END_TIME", "BREAK_MINUTES", "WORKING_DAYS_JSON", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.NAME_AR, src.NAME_EN, src.ST, src.ET, src.BM, src.W_DAYS, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 6. Leave Types
            $"""
            MERGE INTO "{schemaName}"."HR_LEAVE_TYPE" target
            USING (
                SELECT 'ANNUAL' AS CODE, 'إجازة سنوية' AS NAME_AR, 'Annual Leave' AS NAME_EN, 1 AS PAID, 1 AS STAT, 0 AS DOC, 14 AS MD, 1 AS CF, 7 AS CAP FROM DUAL UNION ALL
                SELECT 'SICK', 'إجازة مرضية', 'Sick Leave', 1, 1, 1, 14, 0, 0 FROM DUAL UNION ALL
                SELECT 'MATERNITY', 'إجازة أمومة', 'Maternity Leave', 1, 1, 1, 70, 0, 0 FROM DUAL UNION ALL
                SELECT 'PATERNITY', 'إجازة أبوة', 'Paternity Leave', 1, 1, 1, 3, 0, 0 FROM DUAL UNION ALL
                SELECT 'HAJJ', 'إجازة حج', 'Hajj Leave', 1, 1, 1, 14, 0, 0 FROM DUAL UNION ALL
                SELECT 'BEREAVEMENT', 'إجازة عزاء', 'Bereavement Leave', 1, 1, 0, 3, 0, 0 FROM DUAL UNION ALL
                SELECT 'UNPAID', 'إجازة بدون راتب', 'Unpaid Leave', 0, 0, 0, 30, 0, 0 FROM DUAL
            ) src
            ON (target."LEAVE_TYPE_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."NAME_AR" = src.NAME_AR, target."NAME_EN" = src.NAME_EN, target."MAX_DAYS_PER_YEAR" = src.MD
            WHEN NOT MATCHED THEN
                INSERT ("LEAVE_TYPE_CODE", "NAME_AR", "NAME_EN", "IS_PAID", "IS_STATUTORY", "REQUIRES_DOCUMENTATION", "MAX_DAYS_PER_YEAR", "CARRY_FORWARD_ALLOWED", "CARRY_FORWARD_CAP_DAYS", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.NAME_AR, src.NAME_EN, src.PAID, src.STAT, src.DOC, src.MD, src.CF, src.CAP, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 7. Salary Components
            $"""
            MERGE INTO "{schemaName}"."HR_SALARY_COMPONENT" target
            USING (
                SELECT 'BASIC' AS CODE, 'الراتب الأساسي' AS NAME_AR, 'Basic Salary' AS NAME_EN, 'EARNING' AS C_TYPE, 1 AS TAX, 1 AS SSC, 'FIXED_AMOUNT' AS CALC_T FROM DUAL UNION ALL
                SELECT 'HOUSING', 'بدل سكن', 'Housing Allowance', 'EARNING', 1, 1, 'PERCENTAGE' FROM DUAL UNION ALL
                SELECT 'TRANSPORT', 'بدل مواصلات', 'Transport Allowance', 'EARNING', 1, 1, 'FIXED_AMOUNT' FROM DUAL UNION ALL
                SELECT 'MOBILE', 'بدل هاتف', 'Mobile Allowance', 'EARNING', 1, 0, 'FIXED_AMOUNT' FROM DUAL UNION ALL
                SELECT 'OVERTIME', 'عمل إضافي', 'Overtime Pay', 'EARNING', 1, 0, 'FORMULA' FROM DUAL UNION ALL
                SELECT 'BONUS', 'مكافأة أداء', 'Performance Bonus', 'EARNING', 1, 0, 'FIXED_AMOUNT' FROM DUAL UNION ALL
                SELECT 'UNPAID_LEAVE', 'خصم إجازة غير مدفوعة', 'Unpaid Leave Deduction', 'DEDUCTION', 0, 0, 'FORMULA' FROM DUAL
            ) src
            ON (target."COMPONENT_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."NAME_AR" = src.NAME_AR, target."NAME_EN" = src.NAME_EN
            WHEN NOT MATCHED THEN
                INSERT ("COMPONENT_CODE", "NAME_AR", "NAME_EN", "COMPONENT_TYPE", "IS_TAXABLE", "IS_SSC_APPLICABLE", "CALCULATION_TYPE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.NAME_AR, src.NAME_EN, src.C_TYPE, src.TAX, src.SSC, src.CALC_T, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 8. Employees
            $"""
            MERGE INTO "{schemaName}"."HR_EMPLOYEE" target
            USING (
                SELECT 'EMP-001' AS CODE, 'أحمد محمود منصور' AS NAME_AR, 'Ahmad Mahmoud Mansour' AS NAME_EN, '9851023456' AS NAT_ID, 'HR_DIR' AS POS, 'HR' AS DEPT, 'ahmad.mansour@thinkon.com' AS EMAIL, 'JO94ABCO000000123456789012' AS IBAN, 2 AS TAX_EX, 0 AS RISK FROM DUAL UNION ALL
                SELECT 'EMP-002', 'سارة خالد الخطيب', 'Sara Khaled Al-Khatib', '9902034567', 'SR_SWE', 'ENG', 'sara.khatib@thinkon.com', 'JO94ABCO000000223456789012', 1, 0 FROM DUAL UNION ALL
                SELECT 'EMP-003', 'طارق زياد النجار', 'Tariq Ziad Al-Najjar', '9953045678', 'JR_SWE', 'ENG', 'tariq.najjar@thinkon.com', 'JO94ABCO000000323456789012', 0, 0 FROM DUAL UNION ALL
                SELECT 'EMP-004', 'نور سليم حداد', 'Noor Salim Haddad', '9884056789', 'FIN_ACC', 'FIN', 'noor.haddad@thinkon.com', 'JO94ABCO000000423456789012', 2, 0 FROM DUAL UNION ALL
                SELECT 'EMP-005', 'عمر يوسف قاسم', 'Omar Yousef Qasim', '9925067890', 'OPS_SUP', 'OPS', 'omar.qasim@thinkon.com', 'JO94ABCO000000523456789012', 0, 1 FROM DUAL
            ) src
            ON (target."EMPLOYEE_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."NAME_AR" = src.NAME_AR, target."NAME_EN" = src.NAME_EN, target."EMAIL" = src.EMAIL
            WHEN NOT MATCHED THEN
                INSERT ("EMPLOYEE_CODE", "NAME_AR", "NAME_EN", "NATIONAL_ID", "POSITION_CODE", "DEPARTMENT_CODE", "EMAIL", "BANK_IBAN", "TAX_EXEMPTION_COUNT", "IS_HIGH_RISK_ROLE", "DATE_OF_BIRTH", "HIRE_DATE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.NAME_AR, src.NAME_EN, src.NAT_ID, src.POS, src.DEPT, src.EMAIL, src.IBAN, src.TAX_EX, src.RISK, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 9. Employee Shifts
            $"""
            MERGE INTO "{schemaName}"."HR_EMP_SHIFT_ASSIGNMENT" target
            USING (
                SELECT 'EMP-001' AS EMP, 'GENERAL' AS SHIFT FROM DUAL UNION ALL
                SELECT 'EMP-002', 'GENERAL' FROM DUAL UNION ALL
                SELECT 'EMP-003', 'GENERAL' FROM DUAL UNION ALL
                SELECT 'EMP-004', 'GENERAL' FROM DUAL UNION ALL
                SELECT 'EMP-005', 'GENERAL' FROM DUAL
            ) src
            ON (target."EMPLOYEE_CODE" = src.EMP AND target."SHIFT_CODE" = src.SHIFT)
            WHEN NOT MATCHED THEN
                INSERT ("EMPLOYEE_CODE", "SHIFT_CODE", "EFFECTIVE_FROM", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.EMP, src.SHIFT, CURRENT_TIMESTAMP, 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 10. Salary Structures
            $"""
            MERGE INTO "{schemaName}"."HR_EMP_SALARY_STRUCTURE" target
            USING (
                SELECT 'EMP-001' AS EMP, 3500.00 AS BASIC FROM DUAL UNION ALL
                SELECT 'EMP-002', 2000.00 FROM DUAL UNION ALL
                SELECT 'EMP-003', 700.00 FROM DUAL UNION ALL
                SELECT 'EMP-004', 1600.00 FROM DUAL UNION ALL
                SELECT 'EMP-005', 1100.00 FROM DUAL
            ) src
            ON (target."EMPLOYEE_CODE" = src.EMP)
            WHEN MATCHED THEN
                UPDATE SET target."BASIC_SALARY" = src.BASIC
            WHEN NOT MATCHED THEN
                INSERT ("EMPLOYEE_CODE", "EFFECTIVE_FROM", "BASIC_SALARY", "CURRENCY_CODE", "PAYMENT_METHOD", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.EMP, CURRENT_TIMESTAMP, src.BASIC, 'JOD', 'BANK_TRANSFER', 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 11. Salary Structure Lines
            $"""
            INSERT INTO "{schemaName}"."HR_EMP_SALARY_STRUCT_LINE" ("STRUCTURE_ID", "COMPONENT_CODE", "AMOUNT", "PERCENT_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
            SELECT s."ID", 'HOUSING', s."BASIC_SALARY" * 0.25, 25.00, 1, 'system_seed', CURRENT_TIMESTAMP
            FROM "{schemaName}"."HR_EMP_SALARY_STRUCTURE" s
            WHERE NOT EXISTS (SELECT 1 FROM "{schemaName}"."HR_EMP_SALARY_STRUCT_LINE" l WHERE l."STRUCTURE_ID" = s."ID" AND l."COMPONENT_CODE" = 'HOUSING')
            """,
            $"""
            INSERT INTO "{schemaName}"."HR_EMP_SALARY_STRUCT_LINE" ("STRUCTURE_ID", "COMPONENT_CODE", "AMOUNT", "PERCENT_VALUE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
            SELECT s."ID", 'TRANSPORT', 150.00, NULL, 1, 'system_seed', CURRENT_TIMESTAMP
            FROM "{schemaName}"."HR_EMP_SALARY_STRUCTURE" s
            WHERE NOT EXISTS (SELECT 1 FROM "{schemaName}"."HR_EMP_SALARY_STRUCT_LINE" l WHERE l."STRUCTURE_ID" = s."ID" AND l."COMPONENT_CODE" = 'TRANSPORT')
            """,

            // 12. Employment Contracts
            $"""
            MERGE INTO "{schemaName}"."HR_EMPLOYMENT_CONTRACT" target
            USING (
                SELECT 'EMP-001' AS EMP, 'UNLIMITED' AS C_TYPE FROM DUAL UNION ALL
                SELECT 'EMP-002', 'UNLIMITED' FROM DUAL UNION ALL
                SELECT 'EMP-003', 'LIMITED_2_YEARS' FROM DUAL UNION ALL
                SELECT 'EMP-004', 'UNLIMITED' FROM DUAL UNION ALL
                SELECT 'EMP-005', 'UNLIMITED' FROM DUAL
            ) src
            ON (target."EMPLOYEE_CODE" = src.EMP)
            WHEN NOT MATCHED THEN
                INSERT ("EMPLOYEE_CODE", "CONTRACT_TYPE", "START_DATE", "STATUS", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.EMP, src.C_TYPE, CURRENT_TIMESTAMP, 'ACTIVE', 1, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 13. Leave Balances
            $"""
            MERGE INTO "{schemaName}"."HR_LEAVE_BALANCE" target
            USING (
                SELECT 'EMP-001' AS EMP, 'ANNUAL' AS L_TYPE, 2026 AS YR, 21.00 AS ACCRUED, 4.00 AS USED, 5.00 AS CARRY FROM DUAL UNION ALL
                SELECT 'EMP-001', 'SICK', 2026, 14.00, 1.00, 0.00 FROM DUAL UNION ALL
                SELECT 'EMP-002', 'ANNUAL', 2026, 14.00, 3.00, 2.00 FROM DUAL UNION ALL
                SELECT 'EMP-002', 'SICK', 2026, 14.00, 0.00, 0.00 FROM DUAL UNION ALL
                SELECT 'EMP-003', 'ANNUAL', 2026, 14.00, 2.00, 0.00 FROM DUAL UNION ALL
                SELECT 'EMP-004', 'ANNUAL', 2026, 14.00, 5.00, 4.00 FROM DUAL UNION ALL
                SELECT 'EMP-005', 'ANNUAL', 2026, 14.00, 1.00, 0.00 FROM DUAL
            ) src
            ON (target."EMPLOYEE_CODE" = src.EMP AND target."LEAVE_TYPE_CODE" = src.L_TYPE AND target."YEAR_NO" = src.YR)
            WHEN MATCHED THEN
                UPDATE SET target."ACCRUED_DAYS" = src.ACCRUED, target."USED_DAYS" = src.USED
            WHEN NOT MATCHED THEN
                INSERT ("EMPLOYEE_CODE", "LEAVE_TYPE_CODE", "YEAR_NO", "ACCRUED_DAYS", "USED_DAYS", "CARRIED_FORWARD_DAYS", "CREATION_USER", "CREATION_DATE")
                VALUES (src.EMP, src.L_TYPE, src.YR, src.ACCRUED, src.USED, src.CARRY, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 14. ATS Requisitions & Candidates
            $"""
            MERGE INTO "{schemaName}"."HR_JOB_REQUISITION" target
            USING (
                SELECT 'REQ-2026-001' AS CODE, 'SR_SWE' AS POS, 'ENG' AS DEPT, 2 AS HC, 'OPEN' AS STATUS, 'EMP-001' AS REQ_BY, 'Senior Software Engineer' AS DESC_EN FROM DUAL UNION ALL
                SELECT 'REQ-2026-002', 'OPS_SUP' AS CODE, 'OPS_SUP' AS POS, 'OPS' AS DEPT, 1 AS HC, 'OPEN' AS STATUS, 'EMP-001' AS REQ_BY, 'Operations Supervisor' AS DESC_EN FROM DUAL
            ) src
            ON (target."REQUISITION_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."STATUS" = src.STATUS
            WHEN NOT MATCHED THEN
                INSERT ("REQUISITION_CODE", "POSITION_CODE", "DEPARTMENT_CODE", "HEADCOUNT", "STATUS", "REQUESTED_BY", "JOB_DESCRIPTION", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.POS, src.DEPT, src.HC, src.STATUS, src.REQ_BY, src.DESC_EN, 'system_seed', CURRENT_TIMESTAMP)
            """,
            $"""
            MERGE INTO "{schemaName}"."HR_CANDIDATE" target
            USING (
                SELECT 'CAND-001' AS CODE, 'رامي المصري' AS NAME_AR, 'Rami Al-Masri' AS NAME_EN, 'rami.masri@example.com' AS EMAIL, '+962788889900' AS PHONE FROM DUAL UNION ALL
                SELECT 'CAND-002', 'ليلى الحسن' AS NAME_AR, 'Layla Al-Hasan', 'layla.hasan@example.com', '+962777778899' FROM DUAL
            ) src
            ON (target."CANDIDATE_CODE" = src.CODE)
            WHEN MATCHED THEN
                UPDATE SET target."EMAIL" = src.EMAIL
            WHEN NOT MATCHED THEN
                INSERT ("CANDIDATE_CODE", "NAME_AR", "NAME_EN", "EMAIL", "PHONE", "SOURCE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.CODE, src.NAME_AR, src.NAME_EN, src.EMAIL, src.PHONE, 'DIRECT', 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 15. Payroll Run
            $"""
            MERGE INTO "{schemaName}"."HR_PAYROLL_RUN" target
            USING (
                SELECT '2026-07' AS PERIOD, CURRENT_TIMESTAMP AS R_DATE, 'APPROVED' AS STATUS, 11450.00 AS GROSS, 9785.45 AS NET, 858.75 AS EMP_SSC, 1631.63 AS EMPR_SSC, 805.80 AS TAX, 0.00 AS NAT_CONTRIB, 0.00 AS OTH_DED, 1 AS BR, 1001 AS JV, 'system_seed' AS CALC_BY, 'Ahmad Mansour' AS APPR_BY FROM DUAL
            ) src
            ON (target."PAY_PERIOD" = src.PERIOD AND target."BRANCH_ID" = src.BR)
            WHEN MATCHED THEN
                UPDATE SET target."STATUS" = src.STATUS, target."TOTAL_GROSS_SALARY" = src.GROSS, target."TOTAL_NET_SALARY" = src.NET
            WHEN NOT MATCHED THEN
                INSERT ("PAY_PERIOD", "RUN_DATE", "STATUS", "TOTAL_GROSS_SALARY", "TOTAL_NET_SALARY", "TOTAL_EMPLOYEE_SSC", "TOTAL_EMPLOYER_SSC", "TOTAL_INCOME_TAX", "TOTAL_NATIONAL_CONTRIB", "TOTAL_OTHER_DEDUCTIONS", "BRANCH_ID", "JOURNAL_VOUCHER_ID", "CALCULATED_BY", "CALCULATION_DATE", "APPROVED_BY", "APPROVAL_DATE", "CREATION_USER", "CREATION_DATE")
                VALUES (src.PERIOD, src.R_DATE, src.STATUS, src.GROSS, src.NET, src.EMP_SSC, src.EMPR_SSC, src.TAX, src.NAT_CONTRIB, src.OTH_DED, src.BR, src.JV, src.CALC_BY, CURRENT_TIMESTAMP, src.APPR_BY, CURRENT_TIMESTAMP, 'system_seed', CURRENT_TIMESTAMP)
            """,

            // 16. Assets
            $"""
            MERGE INTO "{schemaName}"."HR_ASSET_ASSIGNMENT" target
            USING (
                SELECT 'AST-LAP-001' AS TAG, 'MacBook Pro 16' AS DESCR, 'LAPTOP' AS CAT, 'MBP2026001' AS SN, 'EMP-001' AS EMP, 'ASSIGNED' AS STATUS FROM DUAL UNION ALL
                SELECT 'AST-LAP-002', 'Dell XPS 15', 'LAPTOP', 'DELL2026002', 'EMP-002', 'ASSIGNED' FROM DUAL UNION ALL
                SELECT 'AST-MON-001', 'Dell UltraSharp 27 4K', 'MONITOR', 'MON2026001', 'EMP-002', 'ASSIGNED' FROM DUAL UNION ALL
                SELECT 'AST-MOB-001', 'iPhone 15 Pro Max', 'MOBILE', 'IPH2026001', 'EMP-001', 'ASSIGNED' FROM DUAL
            ) src
            ON (target."ASSET_TAG" = src.TAG)
            WHEN MATCHED THEN
                UPDATE SET target."STATUS" = src.STATUS
            WHEN NOT MATCHED THEN
                INSERT ("ASSET_TAG", "ASSET_DESCRIPTION", "CATEGORY", "SERIAL_NUMBER", "EMPLOYEE_CODE", "ISSUED_DATE", "STATUS", "CREATION_USER", "CREATION_DATE")
                VALUES (src.TAG, src.DESCR, src.CAT, src.SN, src.EMP, CURRENT_TIMESTAMP, src.STATUS, 'system_seed', CURRENT_TIMESTAMP)
            """
        };

        foreach (var sql in seedStatements)
        {
            try
            {
                await ExecuteRawAsync(connection, sql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notice during direct HR data seeding in {SchemaName}", schemaName);
            }
        }
    }

    private async Task ExecuteAccountingDdlAsync(OracleConnection connection, string objectDescription, string sql)
    {
        try
        {
            await ExecuteRawAsync(connection, sql);
        }
        catch (OracleException ex) when (ex.Number is 955 or 1408 or 1430 or 1442 or 2260 or 2261 or 2264 or 2275 or 2443)
        {
            _logger.LogDebug(
                "Accounting/HR {ObjectDescription} already exists or is already configured in the requested form: {Message}",
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
                await EnsureHrSchemaAsync(masterConn, schema);

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
        await EnsureHrSchemaAsync(masterConn, schemaName);
        
        // Ensure access privileges and synonyms are up to date
        await GrantTenantTableAccessAsync(schemaName, schemaPassword);
        await CreateGlobalSynonymsAsync(schemaName, schemaPassword);
    }

    public async Task ProvisionDeveloperSchemaAsync()
    {
        _logger.LogInformation("Ensuring developer template schema exists: {SchemaName}", _devSchemaName);

        await using var masterConn = await OpenMasterConnectionAsync();

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

        // 4. Apply accounting-specific and HR-specific Oracle DDL (foreign keys, check constraints, defaults, indexes)
        await EnsureAccountingSchemaAsync(masterConn, _devSchemaName);
        await EnsureHrSchemaAsync(masterConn, _devSchemaName);
        
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

        var seedScriptFiles = new[]
        {
            "92_Seed_Developer_Template.sql",
            "93_Seed_DEV_TEMPLATE_COA.sql",
            "99_Create_HR_Payroll_Tables.sql",
            "100_Seed_DEV_TEMPLATE_HR.sql"
        };

        var hashedPassword = _passwordHashingService.HashPassword("Password@123");
        await using var conn = await OpenTenantConnectionAsync(_devSchemaName, _devSchemaPassword);

        foreach (var scriptFileName in seedScriptFiles)
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", "Scripts", scriptFileName);
            if (!File.Exists(scriptPath))
            {
                var dir = AppContext.BaseDirectory;
                while (!string.IsNullOrEmpty(dir))
                {
                    var candidate = Path.Combine(dir, "Database", "Scripts", scriptFileName);
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
                continue;
            }

            _logger.LogInformation("Executing seed script {ScriptFileName} on {SchemaName}", scriptFileName, _devSchemaName);
            var scriptContent = await File.ReadAllTextAsync(scriptPath);
            var rawBlocks = scriptContent
                .Split(new[] { "\r\n/\r\n", "\n/\n", "\r/\r", "\r\n/\n", "\n/\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            foreach (var rawBlock in rawBlocks)
            {
                var block = rawBlock.Replace("'TEMP_HASH'", $"'{hashedPassword}'", StringComparison.OrdinalIgnoreCase).Trim();
                
                // Clean up slash characters if they are still at the end of the block
                if (block.EndsWith("/", StringComparison.Ordinal))
                {
                    block = block.Substring(0, block.Length - 1).Trim();
                }

                if (string.IsNullOrWhiteSpace(block) 
                    || block.StartsWith("SET DEFINE", StringComparison.OrdinalIgnoreCase) 
                    || block.StartsWith("SET SERVEROUTPUT", StringComparison.OrdinalIgnoreCase))
                    continue;

                var trimmedUpper = block.TrimStart().ToUpperInvariant();
                if (trimmedUpper.StartsWith("BEGIN") || trimmedUpper.StartsWith("DECLARE"))
                {
                    try
                    {
                        await ExecuteRawAsync(conn, block);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Notice during PL/SQL execution in {ScriptFileName}: {Stmt}", scriptFileName, block.Length > 200 ? block.Substring(0, 200) + "..." : block);
                    }
                }
                else
                {
                    // Standard SQL statements - split by semicolon if multiple
                    var subStatements = block.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s)
                                 && !s.StartsWith("--", StringComparison.Ordinal)
                                 && !s.Equals("COMMIT", StringComparison.OrdinalIgnoreCase));

                    foreach (var sql in subStatements)
                    {
                        if (sql.StartsWith("--", StringComparison.Ordinal))
                            continue;

                        try
                        {
                            await ExecuteRawAsync(conn, sql);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Notice during SQL statement execution in {ScriptFileName}: {Stmt}", scriptFileName, sql.Length > 200 ? sql.Substring(0, 200) + "..." : sql);
                        }
                    }
                }
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
