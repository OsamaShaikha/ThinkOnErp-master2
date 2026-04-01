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
        var companies = new List<SysCompany>();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_COMPANY_SELECT_ALL";

                // Add output parameter for SYS_REFCURSOR
                var cursorParam = new OracleParameter
                {
                    ParameterName = "P_RESULT_CURSOR",
                    OracleDbType = OracleDbType.RefCursor,
                    Direction = ParameterDirection.Output
                };
                command.Parameters.Add(cursorParam);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        companies.Add(MapToEntity(reader));
                    }
                }
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
    public async Task<SysCompany?> GetByIdAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_COMPANY_SELECT_BY_ID";

                // Add input parameter for ROW_ID
                var idParam = new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = rowId
                };
                command.Parameters.Add(idParam);

                // Add output parameter for SYS_REFCURSOR
                var cursorParam = new OracleParameter
                {
                    ParameterName = "P_RESULT_CURSOR",
                    OracleDbType = OracleDbType.RefCursor,
                    Direction = ParameterDirection.Output
                };
                command.Parameters.Add(cursorParam);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return MapToEntity(reader);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a new company in the database.
    /// Calls SP_SYS_COMPANY_INSERT stored procedure.
    /// </summary>
    /// <param name="company">The company entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_COMPANY sequence</returns>
    public async Task<decimal> CreateAsync(SysCompany company)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_COMPANY_INSERT";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = company.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = company.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_COUNTRY_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)company.CountryId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CURR_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)company.CurrId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CREATION_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = company.CreationUser
                });

                // Add output parameter for new ID
                var newIdParam = new OracleParameter
                {
                    ParameterName = "P_NEW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Output
                };
                command.Parameters.Add(newIdParam);

                await command.ExecuteNonQueryAsync();

                // Return the generated ID
                return Convert.ToDecimal(newIdParam.Value.ToString());
            }
        }
    }

    /// <summary>
    /// Updates an existing company in the database.
    /// Calls SP_SYS_COMPANY_UPDATE stored procedure.
    /// </summary>
    /// <param name="company">The company entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> UpdateAsync(SysCompany company)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_COMPANY_UPDATE";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = company.RowId
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = company.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = company.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_COUNTRY_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)company.CountryId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CURR_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)company.CurrId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_UPDATE_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = company.UpdateUser ?? string.Empty
                });

                return await command.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Performs a soft delete on a company by setting IS_ACTIVE to false.
    /// Calls SP_SYS_COMPANY_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> DeleteAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_COMPANY_DELETE";

                // Add input parameter for ROW_ID
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = rowId
                });

                return await command.ExecuteNonQueryAsync();
            }
        }
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
            RowId = reader.GetDecimal(reader.GetOrdinal("ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            CountryId = reader.IsDBNull(reader.GetOrdinal("COUNTRY_ID")) ? null : reader.GetDecimal(reader.GetOrdinal("COUNTRY_ID")),
            CurrId = reader.IsDBNull(reader.GetOrdinal("CURR_ID")) ? null : reader.GetDecimal(reader.GetOrdinal("CURR_ID")),
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
        return value == "Y" || value == "1";
    }
}
