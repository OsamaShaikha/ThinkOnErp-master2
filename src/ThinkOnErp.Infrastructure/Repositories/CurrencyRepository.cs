using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysCurrency entity using ADO.NET with Oracle stored procedures.
/// Implements ICurrencyRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class CurrencyRepository : ICurrencyRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the CurrencyRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public CurrencyRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all active currencies from the database.
    /// Calls SP_SYS_CURRENCY_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysCurrency entities</returns>
    public async Task<List<SysCurrency>> GetAllAsync()
    {
        var currencies = new List<SysCurrency>();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_CURRENCY_SELECT_ALL";

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
                        currencies.Add(MapToEntity(reader));
                    }
                }
            }
        }

        return currencies;
    }

    /// <summary>
    /// Retrieves a specific currency by its ID.
    /// Calls SP_SYS_CURRENCY_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the currency</param>
    /// <returns>The SysCurrency entity if found, null otherwise</returns>
    public async Task<SysCurrency?> GetByIdAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_CURRENCY_SELECT_BY_ID";

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
    /// Creates a new currency in the database.
    /// Calls SP_SYS_CURRENCY_INSERT stored procedure.
    /// </summary>
    /// <param name="currency">The currency entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_CURRENCY sequence</returns>
    public async Task<decimal> CreateAsync(SysCurrency currency)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_CURRENCY_INSERT";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SHORT_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.ShortDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SHORT_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.ShortDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SINGULER_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SingulerDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SINGULER_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SingulerDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_DUAL_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.DualDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_DUAL_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.DualDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SUM_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SumDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SUM_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SumDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_FRAC_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.FracDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_FRAC_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.FracDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CURR_RATE",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)currency.CurrRate ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CURR_RATE_DATE",
                    OracleDbType = OracleDbType.Date,
                    Direction = ParameterDirection.Input,
                    Value = (object?)currency.CurrRateDate ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CREATION_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.CreationUser
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
    /// Updates an existing currency in the database.
    /// Calls SP_SYS_CURRENCY_UPDATE stored procedure.
    /// </summary>
    /// <param name="currency">The currency entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> UpdateAsync(SysCurrency currency)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_CURRENCY_UPDATE";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = currency.RowId
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SHORT_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.ShortDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SHORT_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.ShortDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SINGULER_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SingulerDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SINGULER_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SingulerDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_DUAL_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.DualDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_DUAL_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.DualDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SUM_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SumDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_SUM_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.SumDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_FRAC_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.FracDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_FRAC_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.FracDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CURR_RATE",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)currency.CurrRate ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CURR_RATE_DATE",
                    OracleDbType = OracleDbType.Date,
                    Direction = ParameterDirection.Input,
                    Value = (object?)currency.CurrRateDate ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_UPDATE_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = currency.UpdateUser ?? string.Empty
                });

                return await command.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Performs a soft delete on a currency by setting IS_ACTIVE to false.
    /// Calls SP_SYS_CURRENCY_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the currency to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> DeleteAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_CURRENCY_DELETE";

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
    /// Maps an OracleDataReader row to a SysCurrency entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysCurrency entity populated with data from the reader</returns>
    private SysCurrency MapToEntity(OracleDataReader reader)
    {
        return new SysCurrency
        {
            RowId = reader.GetDecimal(reader.GetOrdinal("ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            ShortDesc = reader.GetString(reader.GetOrdinal("SHORT_DESC")),
            ShortDescE = reader.GetString(reader.GetOrdinal("SHORT_DESC_E")),
            SingulerDesc = reader.GetString(reader.GetOrdinal("SINGULER_DESC")),
            SingulerDescE = reader.GetString(reader.GetOrdinal("SINGULER_DESC_E")),
            DualDesc = reader.GetString(reader.GetOrdinal("DUAL_DESC")),
            DualDescE = reader.GetString(reader.GetOrdinal("DUAL_DESC_E")),
            SumDesc = reader.GetString(reader.GetOrdinal("SUM_DESC")),
            SumDescE = reader.GetString(reader.GetOrdinal("SUM_DESC_E")),
            FracDesc = reader.GetString(reader.GetOrdinal("FRAC_DESC")),
            FracDescE = reader.GetString(reader.GetOrdinal("FRAC_DESC_E")),
            CurrRate = reader.IsDBNull(reader.GetOrdinal("CURR_RATE")) ? null : reader.GetDecimal(reader.GetOrdinal("CURR_RATE")),
            CurrRateDate = reader.IsDBNull(reader.GetOrdinal("CURR_RATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CURR_RATE_DATE")),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }
}
