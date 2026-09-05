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
        "SYS_FIELD_VALIDATION_RULE",
        "SYS_AUDIT_LOG", "SYS_AUDIT_LOG_ARCHIVE", "SYS_RETENTION_POLICIES", "SYS_AUDIT_INTEGRITY",
        "SYS_REQUEST_TICKET", "SYS_TICKET_TYPE", "SYS_TICKET_PRIORITY", "SYS_TICKET_STATUS",
        "SYS_TICKET_CATEGORY", "SYS_TICKET_COMMENT", "SYS_TICKET_ATTACHMENT", "SYS_TICKET_CONFIG",
        "__EFMigrationsHistory"
    };

    private static readonly HashSet<string> AuditTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS_AUDIT_LOG", "SYS_AUDIT_LOG_ARCHIVE", "SYS_RETENTION_POLICIES", "SYS_AUDIT_INTEGRITY"
    };

    private static readonly HashSet<string> SupportTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS_REQUEST_TICKET", "SYS_TICKET_TYPE", "SYS_TICKET_PRIORITY", "SYS_TICKET_STATUS",
        "SYS_TICKET_CATEGORY", "SYS_TICKET_COMMENT", "SYS_TICKET_ATTACHMENT", "SYS_TICKET_CONFIG"
    };

    private static readonly HashSet<string> SupportSequences = new(StringComparer.OrdinalIgnoreCase)
    {
        "SEQ_SYS_TICKET_CONFIG", "SEQ_SYS_TICKET_PRIORITY", "SEQ_SYS_TICKET_STATUS", "SEQ_SYS_TICKET_TYPE",
        "SEQ_SYS_TICKET_CATEGORY", "SEQ_SYS_REQUEST_TICKET", "SEQ_SYS_TICKET_COMMENT", "SEQ_SYS_TICKET_ATTACHMENT"
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

        // Reset identity sequences for all tables in the new schema dynamically to start from 1
        var resetIdentitySql = $@"
            DECLARE
            BEGIN
                FOR r IN (
                    SELECT table_name, column_name 
                    FROM all_tab_identity_cols 
                    WHERE owner = '{schemaName}'
                ) LOOP
                    BEGIN
                        EXECUTE IMMEDIATE 'ALTER TABLE ""{schemaName}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" GENERATED BY DEFAULT AS IDENTITY (START WITH 1))';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END LOOP;
            END;";
        try
        {
            await ExecuteRawAsync(masterConn, resetIdentitySql);
            _logger.LogDebug("Dynamically reset identity sequences to START WITH 1 for {Schema}", schemaName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dynamic identity sequence reset skipped for {Schema}", schemaName);
        }

        // Ensure baseline accounting reference data
        await EnsureAccountingSchemaAsync(masterConn, schemaName);

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
                $"(\"Id\", \"NAME_LOCAL\", \"NAME_EN\", \"NOTE\", \"IS_ACTIVE\", \"CREATION_USER\", \"CREATION_DATE\") " +
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
                $"(\"Id\", \"NAME_LOCAL\", \"NAME_EN\", \"USER_NAME\", \"PASSWORD\", \"ROLE\", \"COMPANY_ID\", " +
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

        try
        {
            return await ExecuteGetUserAsync(tenantConn, schemaName, userName, "NAME_LOCAL");
        }
        catch (OracleException ex) when (ex.Number == 904)
        {
            return await ExecuteGetUserAsync(tenantConn, schemaName, userName, "NAME_AR");
        }
    }

    private static async Task<SysUser?> ExecuteGetUserAsync(OracleConnection tenantConn, string schemaName, string userName, string nameColumn)
    {
        var sql = $"SELECT u.\"Id\", u.\"{nameColumn}\", u.\"NAME_EN\", u.\"USER_NAME\", u.\"PASSWORD\", u.\"ROLE\", ub.\"BRANCH_ID\", u.\"COMPANY_ID\", " +
                  $"u.\"IS_ACTIVE\", u.\"IS_ADMIN\", u.\"CREATION_USER\", u.\"CREATION_DATE\", u.\"UPDATE_USER\", u.\"UPDATE_DATE\", " +
                  $"u.\"REFRESH_TOKEN\", u.\"REFRESH_TOKEN_EXPIRY\", u.\"FORCE_LOGOUT_DATE\", u.\"PHONE\", u.\"PHONE2\", u.\"EMAIL\", u.\"LAST_LOGIN_DATE\", u.\"DEFAULT_LANG\" " +
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
                FullNameLocal = reader.GetString(1),
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
                LastLoginDate = reader.IsDBNull(20) ? null : reader.GetDateTime(20),
                DefaultLang = reader.IsDBNull(21) ? 1 : Convert.ToInt32(reader.GetValue(21))
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

    private async Task GrantTenantTableAccessAsync(string schemaName, string password)
    {
        await using var tenantConn = await OpenTenantConnectionAsync(schemaName, password);

        var masterUser = "THINKON_ERP";
        var grantSql = $@"
            DECLARE
            BEGIN
                FOR t IN (SELECT table_name FROM user_tables) LOOP
                    BEGIN
                        EXECUTE IMMEDIATE 'GRANT SELECT, INSERT, UPDATE, DELETE ON ""' || t.table_name || '"" TO ""{masterUser}""';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END LOOP;
                FOR s IN (SELECT sequence_name FROM user_sequences) LOOP
                    BEGIN
                        EXECUTE IMMEDIATE 'GRANT SELECT, ALTER ON ""' || s.sequence_name || '"" TO ""{masterUser}""';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END LOOP;
            END;";

        try
        {
            await ExecuteRawAsync(tenantConn, grantSql);
            _logger.LogDebug("Dynamically granted tenant privileges on all objects in {Schema} to {User}", schemaName, masterUser);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dynamically grant tenant table access in schema {Schema}", schemaName);
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

            var targetSchema = AuditTables.Contains(table) ? "THINKON_AUDIT"
                             : SupportTables.Contains(table) ? "THINKON_SUPPORT"
                             : "THINKON_ERP";

            try
            {
                await ExecuteRawAsync(tenantConn,
                    $"CREATE SYNONYM \"{table}\" FOR \"{targetSchema}\".\"{table}\"");
                _logger.LogDebug("Synonym created: {Schema}.{Table} -> {TargetSchema}.{Table}", schemaName, table, targetSchema);
            }
            catch (OracleException ex) when (ex.Number == 955)
            {
                _logger.LogDebug("Synonym already exists: {Schema}.{Table}", schemaName, table);
            }
        }

        // Create synonyms for Support sequences
        foreach (var seq in SupportSequences)
        {
            try
            {
                await ExecuteRawAsync(tenantConn,
                    $"CREATE SYNONYM \"{seq}\" FOR \"THINKON_SUPPORT\".\"{seq}\"");
                _logger.LogDebug("Sequence synonym created: {Schema}.{Seq} -> THINKON_SUPPORT.{Seq}", schemaName, seq);
            }
            catch (OracleException ex) when (ex.Number == 955)
            {
                _logger.LogDebug("Sequence synonym already exists: {Schema}.{Seq}", schemaName, seq);
            }
        }
    }

    /// <summary>
    /// Ensures tenant baseline reference data (account categories, voucher types, cost centers, tax defaults) is populated.
    /// All schema structures, tables, columns, constraints, and indexes are cloned dynamically from DEV_TEMPLATE.
    /// </summary>
    private async Task EnsureAccountingSchemaAsync(OracleConnection connection, string schemaName)
    {
        schemaName = NormalizeOracleIdentifier(schemaName);
        _logger.LogInformation("Ensuring accounting baseline reference data in {SchemaName}", schemaName);

        try
        {
            await SeedAccountCategoriesAsync(connection, schemaName);
            await SeedVoucherTypesAsync(connection, schemaName);
            await SeedCostCentersAsync(connection, schemaName);
            await SeedTaxDefaultsAsync(connection, schemaName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reference seeding warning for schema {SchemaName}", schemaName);
        }
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

    private async Task SeedAccountCategoriesAsync(OracleConnection connection, string schemaName)
    {
        var categories = new (long Id, int Code, string NameLocal, string NameEn, string Balance, string Statement, int Order)[]
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
                       :nameAr AS "NAME_LOCAL",
                       :nameEn AS "NAME_EN",
                       :normalBalance AS "NORMAL_BALANCE",
                       :financialStatement AS "FINANCIAL_STATEMENT",
                       :displayOrder AS "DISPLAY_ORDER"
                FROM DUAL
            ) source
            ON (target."Id" = source."Id")
            WHEN MATCHED THEN UPDATE SET
                target."CATEGORY_CODE" = source."CATEGORY_CODE",
                target."NAME_LOCAL" = source."NAME_LOCAL",
                target."NAME_EN" = source."NAME_EN",
                target."NORMAL_BALANCE" = source."NORMAL_BALANCE",
                target."FINANCIAL_STATEMENT" = source."FINANCIAL_STATEMENT",
                target."DISPLAY_ORDER" = source."DISPLAY_ORDER"
            WHEN NOT MATCHED THEN INSERT
            (
                "Id", "CATEGORY_CODE", "NAME_LOCAL", "NAME_EN",
                "NORMAL_BALANCE", "FINANCIAL_STATEMENT", "DISPLAY_ORDER"
            )
            VALUES
            (
                source."Id", source."CATEGORY_CODE", source."NAME_LOCAL", source."NAME_EN",
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
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 200) { Value = category.NameLocal });
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
        var voucherTypes = new (int Code, string Key, string NameLocal, string NameEn, string Prefix, string Category, string Policy, int Review, int Manual, int System, int Order)[]
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
                       :nameAr AS "NAME_LOCAL",
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
                target."NAME_LOCAL" = source."NAME_LOCAL",
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
                "TYPE_CODE", "TYPE_KEY", "NAME_LOCAL", "NAME_EN", "PREFIX", "CATEGORY",
                "SERIAL_RESET_POLICY", "REQUIRES_REVIEW", "ALLOW_MANUAL_ENTRY", "IS_SYSTEM", "DISPLAY_ORDER", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
            )
            VALUES
            (
                source."TYPE_CODE", source."TYPE_KEY", source."NAME_LOCAL", source."NAME_EN", source."PREFIX", source."CATEGORY",
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
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 200) { Value = vt.NameLocal });
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
        var costCenters = new (string Code, string? ParentCode, string NameLocal, string NameEn, int Level, string Type, int IsPostable)[]
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
                       :nameAr AS "NAME_LOCAL",
                       :nameEn AS "NAME_EN",
                       :ccLevel AS "COST_CENTER_LEVEL",
                       :ccType AS "COST_CENTER_TYPE",
                       :isPostable AS "IS_POSTABLE",
                       'SYSTEM' AS "CREATION_USER"
                FROM DUAL
            ) source
            ON (target."COST_CENTER_CODE" = source."COST_CENTER_CODE")
            WHEN MATCHED THEN UPDATE SET
                target."NAME_LOCAL" = source."NAME_LOCAL",
                target."NAME_EN" = source."NAME_EN",
                target."COST_CENTER_LEVEL" = source."COST_CENTER_LEVEL",
                target."COST_CENTER_TYPE" = source."COST_CENTER_TYPE",
                target."IS_POSTABLE" = source."IS_POSTABLE"
            WHEN NOT MATCHED THEN INSERT
            (
                "COST_CENTER_CODE", "PARENT_COST_CENTER_CODE", "NAME_LOCAL", "NAME_EN",
                "COST_CENTER_LEVEL", "COST_CENTER_TYPE", "IS_POSTABLE", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
            )
            VALUES
            (
                source."COST_CENTER_CODE", source."PARENT_COST_CENTER_CODE", source."NAME_LOCAL", source."NAME_EN",
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
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 200) { Value = cc.NameLocal });
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

    private async Task SeedTaxDefaultsAsync(OracleConnection connection, string schemaName)
    {
        // 1. Categories
        var categories = new (long Id, string Code, string NameLocal, string NameEn, string Description, int Order)[]
        {
            (1, "VAT", "ضريبة القيمة المضافة", "Value Added Tax", "ضريبة القيمة المضافة القياسية والمخفضة والصفرية والمعفاة", 1),
            (2, "WHT", "ضريبة الاستقطاع", "Withholding Tax", "ضريبة الاستقطاع على الخدمات والمدفوعات لغير المقيمين", 2),
            (3, "EXCISE", "الضريبة الانتقائية", "Excise Tax", "الضريبة الانتقائية على السلع المحددة", 3)
        };

        var mergeCategorySql = $"""
            MERGE INTO "{schemaName}"."TAX_CATEGORY" target
            USING
            (
                SELECT :id AS "ID",
                       :code AS "CATEGORY_CODE",
                       :nameAr AS "NAME_LOCAL",
                       :nameEn AS "NAME_EN",
                       :description AS "DESCRIPTION",
                       :displayOrder AS "DISPLAY_ORDER"
                FROM DUAL
            ) source
            ON (target."CATEGORY_CODE" = source."CATEGORY_CODE")
            WHEN MATCHED THEN UPDATE SET
                target."NAME_LOCAL" = source."NAME_LOCAL",
                target."NAME_EN" = source."NAME_EN",
                target."DESCRIPTION" = source."DESCRIPTION",
                target."DISPLAY_ORDER" = source."DISPLAY_ORDER"
            WHEN NOT MATCHED THEN INSERT
            (
                "CATEGORY_CODE", "NAME_LOCAL", "NAME_EN", "DESCRIPTION",
                "DISPLAY_ORDER", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
            )
            VALUES
            (
                source."CATEGORY_CODE", source."NAME_LOCAL", source."NAME_EN", source."DESCRIPTION",
                source."DISPLAY_ORDER", 1, 'SYSTEM', CURRENT_TIMESTAMP
            )
            """;

        // 2. Tax Rates
        var rates = new (string Code, string CatCode, string NameLocal, string NameEn, decimal Percent, string? SalesAcc, string? PurchAcc, int IsExempt, int IsZero, string? ExCode, string? ExAr, string? ExEn, int Order)[]
        {
            ("VAT_15", "VAT", "ضريبة القيمة المضافة القياسية (15%)", "Standard VAT (15%)", 15.0000m, "213101", "113101", 0, 0, null, null, null, 1),
            ("VAT_5", "VAT", "ضريبة القيمة المضافة المخفضة (5%)", "Reduced VAT (5%)", 5.0000m, "213101", "113101", 0, 0, null, null, null, 2),
            ("VAT_0", "VAT", "ضريبة القيمة المضافة الصفرية (0%)", "Zero-Rated VAT (0%)", 0.0000m, "213101", "113101", 0, 1, "VATEX-SA-32", "صادرات أو سلع خاضعة للنسبة الصفرية", "Exports or Zero-rated qualified goods", 3),
            ("VAT_EXEMPT", "VAT", "معفى من ضريبة القيمة المضافة", "Exempt from VAT", 0.0000m, "213101", "113101", 1, 0, "VATEX-SA-29-7", "خدمات مالية أو تأجير عقاري معفى", "Financial services or residential real estate lease exempt", 4),
            ("VAT_OUT_OF_SCOPE", "VAT", "خارج نطاق ضريبة القيمة المضافة", "Out of Scope VAT", 0.0000m, null, null, 1, 0, "VATEX-SA-OOS", "معاملات خارج النطاق الضريبي", "Out of scope transactions", 5)
        };

        var mergeRateSql = $"""
            MERGE INTO "{schemaName}"."TAX_RATE" target
            USING
            (
                SELECT :code AS "TAX_RATE_CODE",
                       (SELECT "ID" FROM "{schemaName}"."TAX_CATEGORY" WHERE "CATEGORY_CODE" = :catCode) AS "TAX_CATEGORY_ID",
                       :nameAr AS "NAME_LOCAL",
                       :nameEn AS "NAME_EN",
                       :percent AS "RATE_PERCENT",
                       'PERCENTAGE' AS "RATE_TYPE",
                       :salesAcc AS "SALES_TAX_GL_ACCOUNT_CODE",
                       :purchAcc AS "PURCHASE_TAX_GL_ACCOUNT_CODE",
                       :isExempt AS "IS_EXEMPT",
                       :isZero AS "IS_ZERO_RATED",
                       :exCode AS "EXEMPTION_REASON_CODE",
                       :exAr AS "EXEMPTION_REASON_LOCAL",
                       :exEn AS "EXEMPTION_REASON_EN",
                       :displayOrder AS "DISPLAY_ORDER"
                FROM DUAL
            ) source
            ON (target."TAX_RATE_CODE" = source."TAX_RATE_CODE")
            WHEN MATCHED THEN UPDATE SET
                target."NAME_LOCAL" = source."NAME_LOCAL",
                target."NAME_EN" = source."NAME_EN",
                target."RATE_PERCENT" = source."RATE_PERCENT",
                target."SALES_TAX_GL_ACCOUNT_CODE" = source."SALES_TAX_GL_ACCOUNT_CODE",
                target."PURCHASE_TAX_GL_ACCOUNT_CODE" = source."PURCHASE_TAX_GL_ACCOUNT_CODE",
                target."IS_EXEMPT" = source."IS_EXEMPT",
                target."IS_ZERO_RATED" = source."IS_ZERO_RATED",
                target."EXEMPTION_REASON_CODE" = source."EXEMPTION_REASON_CODE",
                target."EXEMPTION_REASON_LOCAL" = source."EXEMPTION_REASON_LOCAL",
                target."EXEMPTION_REASON_EN" = source."EXEMPTION_REASON_EN",
                target."DISPLAY_ORDER" = source."DISPLAY_ORDER"
            WHEN NOT MATCHED THEN INSERT
            (
                "TAX_RATE_CODE", "TAX_CATEGORY_ID", "NAME_LOCAL", "NAME_EN", "RATE_PERCENT", "RATE_TYPE",
                "SALES_TAX_GL_ACCOUNT_CODE", "PURCHASE_TAX_GL_ACCOUNT_CODE",
                "IS_EXEMPT", "IS_ZERO_RATED", "EXEMPTION_REASON_CODE", "EXEMPTION_REASON_LOCAL", "EXEMPTION_REASON_EN",
                "DISPLAY_ORDER", "IS_ACTIVE", "CREATION_USER", "CREATION_DATE"
            )
            VALUES
            (
                source."TAX_RATE_CODE", source."TAX_CATEGORY_ID", source."NAME_LOCAL", source."NAME_EN", source."RATE_PERCENT", source."RATE_TYPE",
                source."SALES_TAX_GL_ACCOUNT_CODE", source."PURCHASE_TAX_GL_ACCOUNT_CODE",
                source."IS_EXEMPT", source."IS_ZERO_RATED", source."EXEMPTION_REASON_CODE", source."EXEMPTION_REASON_LOCAL", source."EXEMPTION_REASON_EN",
                source."DISPLAY_ORDER", 1, 'SYSTEM', CURRENT_TIMESTAMP
            )
            """;

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var cat in categories)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.BindByName = true;
                cmd.CommandText = mergeCategorySql;
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int64) { Value = cat.Id });
                cmd.Parameters.Add(new OracleParameter("code", OracleDbType.NVarchar2, 50) { Value = cat.Code });
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 150) { Value = cat.NameLocal });
                cmd.Parameters.Add(new OracleParameter("nameEn", OracleDbType.NVarchar2, 150) { Value = cat.NameEn });
                cmd.Parameters.Add(new OracleParameter("description", OracleDbType.NVarchar2, 500) { Value = cat.Description });
                cmd.Parameters.Add(new OracleParameter("displayOrder", OracleDbType.Int32) { Value = cat.Order });
                await cmd.ExecuteNonQueryAsync();
            }

            foreach (var r in rates)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.BindByName = true;
                cmd.CommandText = mergeRateSql;
                cmd.Parameters.Add(new OracleParameter("code", OracleDbType.NVarchar2, 50) { Value = r.Code });
                cmd.Parameters.Add(new OracleParameter("catCode", OracleDbType.NVarchar2, 50) { Value = r.CatCode });
                cmd.Parameters.Add(new OracleParameter("nameAr", OracleDbType.NVarchar2, 150) { Value = r.NameLocal });
                cmd.Parameters.Add(new OracleParameter("nameEn", OracleDbType.NVarchar2, 150) { Value = r.NameEn });
                cmd.Parameters.Add(new OracleParameter("percent", OracleDbType.Decimal) { Value = r.Percent });
                cmd.Parameters.Add(new OracleParameter("salesAcc", OracleDbType.NVarchar2, 50) { Value = (object?)r.SalesAcc ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("purchAcc", OracleDbType.NVarchar2, 50) { Value = (object?)r.PurchAcc ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("isExempt", OracleDbType.Int32) { Value = r.IsExempt });
                cmd.Parameters.Add(new OracleParameter("isZero", OracleDbType.Int32) { Value = r.IsZero });
                cmd.Parameters.Add(new OracleParameter("exCode", OracleDbType.NVarchar2, 50) { Value = (object?)r.ExCode ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("exAr", OracleDbType.NVarchar2, 300) { Value = (object?)r.ExAr ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("exEn", OracleDbType.NVarchar2, 300) { Value = (object?)r.ExEn ?? DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("displayOrder", OracleDbType.Int32) { Value = r.Order });
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
                _logger.LogWarning(rollbackException, "Failed to roll back tax defaults seed transaction for {SchemaName}", schemaName);
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

        // Dynamically reset identity sequences for all tables to prevent duplicate key errors
        var resetDevSequencesSql = $@"
            DECLARE
            BEGIN
                FOR r IN (
                    SELECT table_name, column_name 
                    FROM all_tab_identity_cols 
                    WHERE owner = '{_devSchemaName}'
                ) LOOP
                    BEGIN
                        EXECUTE IMMEDIATE 'ALTER TABLE ""{_devSchemaName}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" GENERATED BY DEFAULT AS IDENTITY (START WITH LIMIT VALUE))';
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END LOOP;
            END;";
        try
        {
            await ExecuteRawAsync(conn, resetDevSequencesSql);
            _logger.LogDebug("Dynamically reset all identity sequences in {Schema} using START WITH LIMIT VALUE", _devSchemaName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dynamically reset identity sequences in {Schema}", _devSchemaName);
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
        string? branchNameLocal, string? branchNameEn,
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
            (""COMPANY_ID"", ""NAME_LOCAL"", ""NAME_EN"", ""PHONE"", ""MOBILE"", ""FAX"", ""EMAIL"", ""IS_HEAD_BRANCH"", ""TAX_NUMBER"", ""DEFAULT_LANG"", ""BASE_CURRENCY_ID"", ""ROUNDING_RULES"", ""BRANCH_LOGO_PATH"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
            VALUES (:companyId, :nameAr, :nameEn, :phone, :mobile, :fax, :email, 1, :taxNumber, :defaultLang, :baseCurrencyId, :roundingRules, :logoPath, 1, :creationUser, SYSDATE)
            RETURNING ""Id"" INTO :branchId";

        await using var branchCmd = tenantConn.CreateCommand();
        branchCmd.CommandText = branchSql;
        branchCmd.Parameters.Add(new OracleParameter("companyId", companyId));
        branchCmd.Parameters.Add(new OracleParameter("nameAr", branchNameLocal ?? "Default Branch"));
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
        var fyNameLocal = $"السنة المالية {DateTime.Now.Year}";
        var fyNameEn = $"Fiscal Year {DateTime.Now.Year}";
        var startDate = new DateTime(DateTime.Now.Year, 1, 1);
        var endDate = new DateTime(DateTime.Now.Year, 12, 31);

        var fySql = $@"
            INSERT INTO ""{schemaName}"".""SYS_FISCAL_YEAR""
            (""COMPANY_ID"", ""BRANCH_ID"", ""FISCAL_YEAR_CODE"", ""NAME_LOCAL"", ""NAME_EN"", ""START_DATE"", ""END_DATE"", ""IS_CLOSED"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
            VALUES (:companyId, :branchId, :fyCode, :nameAr, :nameEn, :startDate, :endDate, 0, 1, :creationUser, SYSDATE)
            RETURNING ""Id"" INTO :fyId";

        await using var fyCmd = tenantConn.CreateCommand();
        fyCmd.CommandText = fySql;
        fyCmd.Parameters.Add(new OracleParameter("companyId", companyId));
        fyCmd.Parameters.Add(new OracleParameter("branchId", branchId));
        fyCmd.Parameters.Add(new OracleParameter("fyCode", fyCode));
        fyCmd.Parameters.Add(new OracleParameter("nameAr", fyNameLocal));
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

    public async Task<(string? BranchNameEn, string? BranchNameLocal, string? BranchLogoPath)> GetBranchDetailsAsync(string schemaName, long branchId)
    {
        if (string.IsNullOrEmpty(schemaName) || branchId <= 0) return (null, null, null);
        try
        {
            await using var conn = await OpenMasterConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT NAME_EN, NAME_LOCAL, BRANCH_LOGO_PATH
                FROM ""{schemaName.ToUpperInvariant()}"".""SYS_BRANCH""
                WHERE ""Id"" = :branchId";
            cmd.Parameters.Add(new OracleParameter("branchId", branchId));
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var nameEn = reader.IsDBNull(0) ? null : reader.GetString(0);
                var nameLocal = reader.IsDBNull(1) ? null : reader.GetString(1);
                var logoPath = reader.IsDBNull(2) ? null : reader.GetString(2);
                return (nameEn, nameLocal, logoPath);
            }
        }
        catch (OracleException ex) when (ex.Number == 904)
        {
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
                    var nameLocal = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var logoPath = reader.IsDBNull(2) ? null : reader.GetString(2);
                    return (nameEn, nameLocal, logoPath);
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch branch details for schema {Schema}, branchId {BranchId}", schemaName, branchId);
        }
        return (null, null, null);
    }
}
