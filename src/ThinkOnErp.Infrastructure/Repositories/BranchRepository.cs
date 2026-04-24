using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysBranch entity using ADO.NET with Oracle stored procedures.
/// Implements IBranchRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class BranchRepository : IBranchRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the BranchRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public BranchRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all active branches from the database.
    /// Calls SP_SYS_BRANCH_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysBranch entities</returns>
    public async Task<List<SysBranch>> GetAllAsync()
    {
        List<SysBranch> branches = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_BRANCH_SELECT_ALL";

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
                branches.Add(MapToEntity(reader));
            }
        }

        return branches;
    }

    /// <summary>
    /// Retrieves a specific branch by its ID.
    /// Calls SP_SYS_BRANCH_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <returns>The SysBranch entity if found, null otherwise</returns>
    public async Task<SysBranch?> GetByIdAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SELECT_BY_ID";

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
    /// Creates a new branch in the database.
    /// Calls SP_SYS_BRANCH_INSERT stored procedure.
    /// </summary>
    /// <param name="branch">The branch entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_BRANCH sequence</returns>
    public async Task<long> CreateAsync(SysBranch branch)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_INSERT";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PAR_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.ParRowId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.RowDesc
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.RowDescE
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PHONE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Phone ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_MOBILE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Mobile ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FAX",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Fax ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_EMAIL",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Email ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_IS_HEAD_BRANCH",
            OracleDbType = OracleDbType.Char,
            Direction = ParameterDirection.Input,
            Value = branch.IsHeadBranch ? "1" : "0"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DEFAULT_LANG",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.DefaultLang ?? "ar"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BASE_CURRENCY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.BaseCurrencyId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROUNDING_RULES",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.RoundingRules ?? 1
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.CreationUser
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
        return long.Parse(newIdParam.Value.ToString());
    }

    /// <summary>
    /// Updates an existing branch in the database.
    /// Calls SP_SYS_BRANCH_UPDATE stored procedure.
    /// </summary>
    /// <param name="branch">The branch entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> UpdateAsync(SysBranch branch)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_UPDATE";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branch.RowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PAR_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.ParRowId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.RowDesc
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.RowDescE
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PHONE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Phone ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_MOBILE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Mobile ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FAX",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Fax ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_EMAIL",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.Email ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_IS_HEAD_BRANCH",
            OracleDbType = OracleDbType.Char,
            Direction = ParameterDirection.Input,
            Value = branch.IsHeadBranch ? "1" : "0"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DEFAULT_LANG",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.DefaultLang ?? "ar"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BASE_CURRENCY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.BaseCurrencyId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROUNDING_RULES",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)branch.RoundingRules ?? 1
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = branch.UpdateUser ?? string.Empty
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Performs a soft delete on a branch by setting IS_ACTIVE to false.
    /// Calls SP_SYS_BRANCH_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_DELETE";

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
    /// Retrieves all active branches for a specific company.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <returns>A list of SysBranch entities belonging to the specified company</returns>
    public async Task<List<SysBranch>> GetByCompanyIdAsync(long companyId)
    {
        List<SysBranch> branches = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SELECT_BY_COMPANY";

        // Add input parameter for company ID
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = companyId
        });

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
            branches.Add(MapToEntity(reader));
        }

        return branches;
    }

    /// <summary>
    /// Updates the branch logo.
    /// Calls SP_SYS_BRANCH_UPDATE_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <param name="logo">The logo image as byte array</param>
    /// <param name="userName">The username of the user updating the logo</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> UpdateLogoAsync(long rowId, byte[] logo, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_UPDATE_LOGO";

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
            ParameterName = "P_BRANCH_LOGO",
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
    /// Retrieves the branch logo.
    /// Calls SP_SYS_BRANCH_GET_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <returns>The logo image as byte array, null if not found</returns>
    public async Task<byte[]?> GetLogoAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_GET_LOGO";

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
            int logoOrdinal = reader.GetOrdinal("BRANCH_LOGO");
            if (!reader.IsDBNull(logoOrdinal))
            {
                return (byte[])reader.GetValue(logoOrdinal);
            }
        }

        return null;
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysBranch entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysBranch entity populated with data from the reader</returns>
    private SysBranch MapToEntity(OracleDataReader reader)
    {
        var branch = new SysBranch
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            ParRowId = reader.IsDBNull(reader.GetOrdinal("PAR_ROW_ID")) ? null : reader.GetInt64(reader.GetOrdinal("PAR_ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            Phone = reader.IsDBNull(reader.GetOrdinal("PHONE")) ? null : reader.GetString(reader.GetOrdinal("PHONE")),
            Mobile = reader.IsDBNull(reader.GetOrdinal("MOBILE")) ? null : reader.GetString(reader.GetOrdinal("MOBILE")),
            Fax = reader.IsDBNull(reader.GetOrdinal("FAX")) ? null : reader.GetString(reader.GetOrdinal("FAX")),
            Email = reader.IsDBNull(reader.GetOrdinal("EMAIL")) ? null : reader.GetString(reader.GetOrdinal("EMAIL")),
            IsHeadBranch = MapIsHeadBranchToBoolean(reader.GetString(reader.GetOrdinal("IS_HEAD_BRANCH"))),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };

        // Map the new fields (with fallback for older stored procedures)
        try
        {
            var defaultLangOrdinal = reader.GetOrdinal("DEFAULT_LANG");
            branch.DefaultLang = reader.IsDBNull(defaultLangOrdinal) ? null : reader.GetString(defaultLangOrdinal);
        }
        catch (IndexOutOfRangeException)
        {
            // DEFAULT_LANG field not present in this query
            branch.DefaultLang = null;
        }

        try
        {
            var baseCurrencyIdOrdinal = reader.GetOrdinal("BASE_CURRENCY_ID");
            branch.BaseCurrencyId = reader.IsDBNull(baseCurrencyIdOrdinal) ? null : reader.GetInt64(baseCurrencyIdOrdinal);
        }
        catch (IndexOutOfRangeException)
        {
            // BASE_CURRENCY_ID field not present in this query
            branch.BaseCurrencyId = null;
        }

        try
        {
            var roundingRulesOrdinal = reader.GetOrdinal("ROUNDING_RULES");
            branch.RoundingRules = reader.IsDBNull(roundingRulesOrdinal) ? null : reader.GetInt32(roundingRulesOrdinal);
        }
        catch (IndexOutOfRangeException)
        {
            // ROUNDING_RULES field not present in this query
            branch.RoundingRules = null;
        }

        // Set BranchLogo to null but indicate if logo exists via HAS_LOGO field (for performance)
        // The actual logo bytes are not loaded in list queries for performance reasons
        try
        {
            var hasLogoOrdinal = reader.GetOrdinal("HAS_LOGO");
            if (!reader.IsDBNull(hasLogoOrdinal))
            {
                var hasLogo = reader.GetString(hasLogoOrdinal) == "Y";
                // We don't load the actual logo bytes here for performance
                // The HasLogo property will be calculated from this information
                branch.BranchLogo = hasLogo ? new byte[1] : null; // Placeholder to indicate logo exists
            }
        }
        catch (IndexOutOfRangeException)
        {
            // HAS_LOGO field not present in this query (e.g., older stored procedures)
            // Leave BranchLogo as null
        }

        return branch;
    }

    /// <summary>
    /// Maps Oracle IS_HEAD_BRANCH values to C# boolean.
    /// Converts '1' to true, '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_HEAD_BRANCH value</param>
    /// <returns>True if value is '1', false otherwise</returns>
    private bool MapIsHeadBranchToBoolean(string value)
    {
        return value == "1";
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
