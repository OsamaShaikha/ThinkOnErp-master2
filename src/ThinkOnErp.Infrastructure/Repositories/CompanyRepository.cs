using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
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
    /// Creates a new company with an automatic default branch in a single transaction.
    /// Calls SP_SYS_COMPANY_INSERT_WITH_BRANCH stored procedure.
    /// </summary>
    public async Task<(long CompanyId, long BranchId)> CreateWithBranchAsync(
        string? companyNameAr,
        string companyNameEn,
        string? legalNameAr,
        string legalNameEn,
        string companyCode,
        string? taxNumber,
        long? fiscalYearId,
        long? countryId,
        long? currId,
        string? branchNameAr,
        string? branchNameEn,
        string? branchPhone,
        string? branchMobile,
        string? branchFax,
        string? branchEmail,
        byte[]? branchLogo,
        string? defaultLang,
        long? baseCurrencyId,
        int? roundingRules,
        string creationUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_INSERT_WITH_BRANCH";

        // Company Parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = companyNameAr ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = companyNameEn
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_LEGAL_NAME",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = legalNameAr ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_LEGAL_NAME_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = legalNameEn
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_CODE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = companyCode
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TAX_NUMBER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = taxNumber ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FISCAL_YEAR_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = fiscalYearId ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COUNTRY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = countryId ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CURR_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = currId ?? (object)DBNull.Value
        });

        // Branch Parameters (including migrated fields)
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branchNameAr ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branchNameEn ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_PHONE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branchPhone ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_MOBILE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branchMobile ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_FAX",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branchFax ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_EMAIL",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branchEmail ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_LOGO",
            OracleDbType = OracleDbType.Blob,
            Direction = ParameterDirection.Input,
            Value = branchLogo ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DEFAULT_LANG",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = defaultLang ?? "ar"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BASE_CURRENCY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = baseCurrencyId ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROUNDING_RULES",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)roundingRules ?? 1
        });

        // Common Parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = creationUser
        });

        // Output Parameters
        OracleParameter companyIdParam = new()
        {
            ParameterName = "P_NEW_COMPANY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(companyIdParam);

        OracleParameter branchIdParam = new()
        {
            ParameterName = "P_NEW_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(branchIdParam);

        try
        {
            await command.ExecuteNonQueryAsync();

            // Extract the output values
            var companyId = Convert.ToInt64(((OracleDecimal)companyIdParam.Value).Value);
            var branchId = Convert.ToInt64(((OracleDecimal)branchIdParam.Value).Value);

            return (companyId, branchId);
        }
        catch (OracleException ex) when (ex.Number == 20308)
        {
            throw new InvalidOperationException($"Company code '{companyCode}' already exists.");
        }
        catch (OracleException ex) when (ex.Number >= 20301 && ex.Number <= 20313)
        {
            throw new ArgumentException(ex.Message.Substring(ex.Message.IndexOf(':') + 1).Trim());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create company with branch: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets the default branch for a company.
    /// Calls SP_SYS_COMPANY_SET_DEFAULT_BRANCH stored procedure.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <param name="branchId">The unique identifier of the branch to set as default</param>
    /// <param name="userName">The username of the user making the change</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> SetDefaultBranchAsync(long companyId, long branchId, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_SET_DEFAULT_BRANCH";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = companyId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
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
    /// Maps an OracleDataReader row to a SysCompany entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysCompany entity populated with data from the reader</returns>
    private SysCompany MapToEntity(OracleDataReader reader)
    {
        var company = new SysCompany
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            LegalName = reader.IsDBNull(reader.GetOrdinal("LEGAL_NAME")) ? null : reader.GetString(reader.GetOrdinal("LEGAL_NAME")),
            LegalNameE = reader.IsDBNull(reader.GetOrdinal("LEGAL_NAME_E")) ? null : reader.GetString(reader.GetOrdinal("LEGAL_NAME_E")),
            CompanyCode = reader.IsDBNull(reader.GetOrdinal("COMPANY_CODE")) ? null : reader.GetString(reader.GetOrdinal("COMPANY_CODE")),
            TaxNumber = reader.IsDBNull(reader.GetOrdinal("TAX_NUMBER")) ? null : reader.GetString(reader.GetOrdinal("TAX_NUMBER")),
            FiscalYearId = reader.IsDBNull(reader.GetOrdinal("FISCAL_YEAR_ID")) ? null : reader.GetInt64(reader.GetOrdinal("FISCAL_YEAR_ID")),
            CountryId = reader.IsDBNull(reader.GetOrdinal("COUNTRY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("COUNTRY_ID")),
            CurrId = reader.IsDBNull(reader.GetOrdinal("CURR_ID")) ? null : reader.GetInt64(reader.GetOrdinal("CURR_ID")),
            DefaultBranchId = reader.IsDBNull(reader.GetOrdinal("DEFAULT_BRANCH_ID")) ? null : reader.GetInt64(reader.GetOrdinal("DEFAULT_BRANCH_ID")),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };

        // Set CompanyLogo to null but indicate if logo exists via HAS_LOGO field (for performance)
        // The actual logo bytes are not loaded in list queries for performance reasons
        try
        {
            var hasLogoOrdinal = reader.GetOrdinal("HAS_LOGO");
            if (!reader.IsDBNull(hasLogoOrdinal))
            {
                var hasLogo = reader.GetString(hasLogoOrdinal) == "Y";
                // We don't load the actual logo bytes here for performance
                // The HasLogo property will be calculated from this information
                company.CompanyLogo = hasLogo ? new byte[1] : null; // Placeholder to indicate logo exists
            }
        }
        catch (IndexOutOfRangeException)
        {
            // HAS_LOGO field not present in this query (e.g., older stored procedures)
            // Leave CompanyLogo as null
        }

        return company;
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
