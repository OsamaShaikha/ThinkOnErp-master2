using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysFiscalYear entity using ADO.NET with Oracle stored procedures.
/// Implements IFiscalYearRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class FiscalYearRepository : IFiscalYearRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the FiscalYearRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public FiscalYearRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all active fiscal years from the database.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysFiscalYear entities</returns>
    public async Task<List<SysFiscalYear>> GetAllAsync()
    {
        List<SysFiscalYear> fiscalYears = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_FISCAL_YEAR_SELECT_ALL";

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
                fiscalYears.Add(MapToEntity(reader));
            }
        }

        return fiscalYears;
    }

    /// <summary>
    /// Retrieves a specific fiscal year by its ID.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year</param>
    /// <returns>The SysFiscalYear entity if found, null otherwise</returns>
    public async Task<SysFiscalYear?> GetByIdAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_FISCAL_YEAR_SELECT_BY_ID";

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
    /// Retrieves all fiscal years for a specific company.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY stored procedure.
    /// </summary>
    /// <param name="companyId">The company ID to retrieve fiscal years for</param>
    /// <returns>A list of SysFiscalYear entities for the specified company</returns>
    public async Task<List<SysFiscalYear>> GetByCompanyIdAsync(long companyId)
    {
        List<SysFiscalYear> fiscalYears = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY";

            // Add input parameter for COMPANY_ID
            OracleParameter companyIdParam = new()
            {
                ParameterName = "P_COMPANY_ID",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = companyId
            };
            _ = command.Parameters.Add(companyIdParam);

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
                fiscalYears.Add(MapToEntity(reader));
            }
        }

        return fiscalYears;
    }

    /// <summary>
    /// Retrieves all fiscal years for a specific branch.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH stored procedure.
    /// </summary>
    /// <param name="branchId">The branch ID to retrieve fiscal years for</param>
    /// <returns>A list of SysFiscalYear entities for the specified branch</returns>
    public async Task<List<SysFiscalYear>> GetByBranchIdAsync(long branchId)
    {
        List<SysFiscalYear> fiscalYears = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH";

            // Add input parameter for BRANCH_ID
            OracleParameter branchIdParam = new()
            {
                ParameterName = "P_BRANCH_ID",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = branchId
            };
            _ = command.Parameters.Add(branchIdParam);

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
                fiscalYears.Add(MapToEntity(reader));
            }
        }

        return fiscalYears;
    }

    /// <summary>
    /// Creates a new fiscal year in the database.
    /// Calls SP_SYS_FISCAL_YEAR_INSERT stored procedure.
    /// </summary>
    /// <param name="fiscalYear">The fiscal year entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_FISCAL_YEAR sequence</returns>
    public async Task<long> CreateAsync(SysFiscalYear fiscalYear)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_FISCAL_YEAR_INSERT";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.CompanyId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.BranchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FISCAL_YEAR_CODE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.FiscalYearCode
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)fiscalYear.RowDesc ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)fiscalYear.RowDescE ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_START_DATE",
            OracleDbType = OracleDbType.Date,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.StartDate
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_END_DATE",
            OracleDbType = OracleDbType.Date,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.EndDate
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_IS_CLOSED",
            OracleDbType = OracleDbType.Char,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.IsClosed ? "1" : "0"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.CreationUser
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
    /// Updates an existing fiscal year in the database.
    /// Calls SP_SYS_FISCAL_YEAR_UPDATE stored procedure.
    /// </summary>
    /// <param name="fiscalYear">The fiscal year entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> UpdateAsync(SysFiscalYear fiscalYear)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_FISCAL_YEAR_UPDATE";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.RowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.CompanyId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.BranchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FISCAL_YEAR_CODE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.FiscalYearCode
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)fiscalYear.RowDesc ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)fiscalYear.RowDescE ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_START_DATE",
            OracleDbType = OracleDbType.Date,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.StartDate
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_END_DATE",
            OracleDbType = OracleDbType.Date,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.EndDate
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_IS_CLOSED",
            OracleDbType = OracleDbType.Char,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.IsClosed ? "1" : "0"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = fiscalYear.UpdateUser ?? string.Empty
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Performs a soft delete on a fiscal year by setting IS_ACTIVE to false.
    /// Calls SP_SYS_FISCAL_YEAR_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_FISCAL_YEAR_DELETE";

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
    /// Closes a fiscal year by setting IS_CLOSED to true.
    /// Calls SP_SYS_FISCAL_YEAR_CLOSE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year to close</param>
    /// <param name="userName">The username of the user closing the fiscal year</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> CloseAsync(long rowId, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_FISCAL_YEAR_CLOSE";

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
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = userName
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysFiscalYear entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysFiscalYear entity populated with data from the reader</returns>
    private SysFiscalYear MapToEntity(OracleDataReader reader)
    {
        return new SysFiscalYear
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            CompanyId = reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
            BranchId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            FiscalYearCode = reader.GetString(reader.GetOrdinal("FISCAL_YEAR_CODE")),
            RowDesc = reader.IsDBNull(reader.GetOrdinal("ROW_DESC")) ? null : reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.IsDBNull(reader.GetOrdinal("ROW_DESC_E")) ? null : reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            StartDate = reader.GetDateTime(reader.GetOrdinal("START_DATE")),
            EndDate = reader.GetDateTime(reader.GetOrdinal("END_DATE")),
            IsClosed = MapIsClosedToBoolean(reader.GetString(reader.GetOrdinal("IS_CLOSED"))),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }

    /// <summary>
    /// Maps Oracle IS_ACTIVE values to C# boolean.
    /// Converts '1' to true, '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_ACTIVE value</param>
    /// <returns>True if value is '1', false otherwise</returns>
    private bool MapIsActiveToBoolean(string value)
    {
        return value == "1";
    }

    /// <summary>
    /// Maps Oracle IS_CLOSED values to C# boolean.
    /// Converts '1' to true, '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_CLOSED value</param>
    /// <returns>True if value is '1', false otherwise</returns>
    private bool MapIsClosedToBoolean(string value)
    {
        return value == "1";
    }
}
