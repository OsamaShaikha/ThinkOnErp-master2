using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysCompany entity using ADO.NET with Oracle stored procedures.
/// Implements ICompanyRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class CompanyRepository : ICompanyRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the CompanyRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public CompanyRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all active companies from the database.
    /// Calls SP_SYS_COMPANY_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysCompany entities</returns>
    public async Task<List<SysCompany>> GetAllAsync()
    {
        List<SysCompany> companies = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_COMPANY_SELECT_ALL";

            // Add output parameter for SYS_REFCURSOR
            OracleParameter cursorParam = new()
            {
                ParameterName = "P_RESULT_CURSOR",
                OracleDbType = OracleDbType.RefCursor,
                Direction = ParameterDirection.Output
            };
            _ = command.Parameters.Add(cursorParam);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                companies.Add(MapToEntity(reader));
            }
        }

        return companies;
    }

    /// <summary>
    /// Retrieves a specific company by its ID.
    /// Calls SP_SYS_COMPANY_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The SysCompany entity if found, null otherwise</returns>
    public async Task<SysCompany?> GetByIdAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_SELECT_BY_ID";

        // Add input parameter for ROW_ID
        OracleParameter idParam = new()
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        };
        _ = command.Parameters.Add(idParam);

        // Add output parameter for SYS_REFCURSOR
        OracleParameter cursorParam = new()
        {
            ParameterName = "P_RESULT_CURSOR",
            OracleDbType = OracleDbType.RefCursor,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(cursorParam);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToEntity(reader);
        }

        return null;
    }

    /// <summary>
    /// Creates a new company in the database.
    /// Calls SP_SYS_COMPANY_INSERT stored procedure.
    /// </summary>
    /// <param name="company">The company entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_COMPANY sequence</returns>
    public async Task<long> CreateAsync(SysCompany company)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_INSERT";

        // Add input parameters - original fields
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = company.RowDesc
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = company.RowDescE
        });

        // Add input parameters - new fields
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_LEGAL_NAME",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.LegalName ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_LEGAL_NAME_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.LegalNameE ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_CODE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.CompanyCode ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DEFAULT_LANG",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.DefaultLang ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TAX_NUMBER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.TaxNumber ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FISCAL_YEAR_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.FiscalYearId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BASE_CURRENCY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.BaseCurrencyId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SYSTEM_LANGUAGE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.SystemLanguage ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROUNDING_RULES",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.RoundingRules ?? DBNull.Value
        });

        // Original fields
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COUNTRY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.CountryId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CURR_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.CurrId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = company.CreationUser
        });

        // Add output parameter for new ID
        OracleParameter newIdParam = new()
        {
            ParameterName = "P_NEW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(newIdParam);

        _ = await command.ExecuteNonQueryAsync();

        // Return the generated ID
        return long.Parse(newIdParam.Value.ToString()!);
    }

    /// <summary>
    /// Updates an existing company in the database.
    /// Calls SP_SYS_COMPANY_UPDATE stored procedure.
    /// </summary>
    /// <param name="company">The company entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> UpdateAsync(SysCompany company)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_UPDATE";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = company.RowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = company.RowDesc
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = company.RowDescE
        });

        // New fields
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_LEGAL_NAME",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.LegalName ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_LEGAL_NAME_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.LegalNameE ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_CODE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.CompanyCode ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DEFAULT_LANG",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.DefaultLang ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TAX_NUMBER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.TaxNumber ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FISCAL_YEAR_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.FiscalYearId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BASE_CURRENCY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.BaseCurrencyId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SYSTEM_LANGUAGE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.SystemLanguage ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROUNDING_RULES",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)company.RoundingRules ?? DBNull.Value
        });

        // Original fields
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COUNTRY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.CountryId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CURR_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)company.CurrId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = company.UpdateUser ?? string.Empty
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Performs a soft delete on a company by setting IS_ACTIVE to false.
    /// Calls SP_SYS_COMPANY_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_DELETE";

        // Add input parameter for ROW_ID
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the company logo.
    /// Calls SP_SYS_COMPANY_UPDATE_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <param name="logo">The logo image as byte array</param>
    /// <param name="userName">The username of the user updating the logo</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> UpdateLogoAsync(long rowId, byte[] logo, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_UPDATE_LOGO";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_LOGO",
            OracleDbType = OracleDbType.Blob,
            Direction = ParameterDirection.Input,
            Value = logo
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = userName
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieves the company logo.
    /// Calls SP_SYS_COMPANY_GET_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The logo image as byte array, null if not found</returns>
    public async Task<byte[]?> GetLogoAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_GET_LOGO";

        // Add input parameter for ROW_ID
        OracleParameter idParam = new()
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        };
        _ = command.Parameters.Add(idParam);

        // Add output parameter for SYS_REFCURSOR
        OracleParameter cursorParam = new()
        {
            ParameterName = "P_RESULT_CURSOR",
            OracleDbType = OracleDbType.RefCursor,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(cursorParam);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int logoOrdinal = reader.GetOrdinal("COMPANY_LOGO");
            if (!reader.IsDBNull(logoOrdinal))
            {
                return (byte[])reader.GetValue(logoOrdinal);
            }
        }

        return null;
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysCompany entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysCompany entity populated with data from the reader</returns>
    private SysCompany MapToEntity(OracleDataReader reader)
    {
        return new SysCompany
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            LegalName = reader.IsDBNull(reader.GetOrdinal("LEGAL_NAME")) ? null : reader.GetString(reader.GetOrdinal("LEGAL_NAME")),
            LegalNameE = reader.IsDBNull(reader.GetOrdinal("LEGAL_NAME_E")) ? null : reader.GetString(reader.GetOrdinal("LEGAL_NAME_E")),
            CompanyCode = reader.IsDBNull(reader.GetOrdinal("COMPANY_CODE")) ? null : reader.GetString(reader.GetOrdinal("COMPANY_CODE")),
            DefaultLang = reader.IsDBNull(reader.GetOrdinal("DEFAULT_LANG")) ? null : reader.GetString(reader.GetOrdinal("DEFAULT_LANG")),
            TaxNumber = reader.IsDBNull(reader.GetOrdinal("TAX_NUMBER")) ? null : reader.GetString(reader.GetOrdinal("TAX_NUMBER")),
            FiscalYearId = reader.IsDBNull(reader.GetOrdinal("FISCAL_YEAR_ID")) ? null : reader.GetInt64(reader.GetOrdinal("FISCAL_YEAR_ID")),
            BaseCurrencyId = reader.IsDBNull(reader.GetOrdinal("BASE_CURRENCY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("BASE_CURRENCY_ID")),
            SystemLanguage = reader.IsDBNull(reader.GetOrdinal("SYSTEM_LANGUAGE")) ? null : reader.GetString(reader.GetOrdinal("SYSTEM_LANGUAGE")),
            RoundingRules = reader.IsDBNull(reader.GetOrdinal("ROUNDING_RULES")) ? null : reader.GetString(reader.GetOrdinal("ROUNDING_RULES")),
            CountryId = reader.IsDBNull(reader.GetOrdinal("COUNTRY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("COUNTRY_ID")),
            CurrId = reader.IsDBNull(reader.GetOrdinal("CURR_ID")) ? null : reader.GetInt64(reader.GetOrdinal("CURR_ID")),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }

    /// <summary>
    /// Maps Oracle IS_ACTIVE values to C# boolean.
    /// Converts 'Y' or '1' to true, 'N' or '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_ACTIVE value</param>
    /// <returns>True if value is 'Y' or '1', false otherwise</returns>
    private bool MapIsActiveToBoolean(string value)
    {
        return value is "Y" or "1";
    }
}
