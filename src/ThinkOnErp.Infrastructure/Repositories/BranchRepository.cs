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
        var branches = new List<SysBranch>();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_BRANCH_SELECT_ALL";

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
                        branches.Add(MapToEntity(reader));
                    }
                }
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
    public async Task<SysBranch?> GetByIdAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_BRANCH_SELECT_BY_ID";

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
    /// Creates a new branch in the database.
    /// Calls SP_SYS_BRANCH_INSERT stored procedure.
    /// </summary>
    /// <param name="branch">The branch entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_BRANCH sequence</returns>
    public async Task<decimal> CreateAsync(SysBranch branch)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_BRANCH_INSERT";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PAR_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.ParRowId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = branch.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = branch.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PHONE",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Phone ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_MOBILE",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Mobile ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_FAX",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Fax ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_EMAIL",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Email ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_IS_HEAD_BRANCH",
                    OracleDbType = OracleDbType.Char,
                    Direction = ParameterDirection.Input,
                    Value = branch.IsHeadBranch ? "1" : "0"
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CREATION_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = branch.CreationUser
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
    /// Updates an existing branch in the database.
    /// Calls SP_SYS_BRANCH_UPDATE stored procedure.
    /// </summary>
    /// <param name="branch">The branch entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> UpdateAsync(SysBranch branch)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_BRANCH_UPDATE";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = branch.RowId
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PAR_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.ParRowId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = branch.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = branch.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PHONE",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Phone ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_MOBILE",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Mobile ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_FAX",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Fax ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_EMAIL",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)branch.Email ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_IS_HEAD_BRANCH",
                    OracleDbType = OracleDbType.Char,
                    Direction = ParameterDirection.Input,
                    Value = branch.IsHeadBranch ? "1" : "0"
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_UPDATE_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = branch.UpdateUser ?? string.Empty
                });

                return await command.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Performs a soft delete on a branch by setting IS_ACTIVE to false.
    /// Calls SP_SYS_BRANCH_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> DeleteAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_BRANCH_DELETE";

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
    /// Maps an OracleDataReader row to a SysBranch entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysBranch entity populated with data from the reader</returns>
    private SysBranch MapToEntity(OracleDataReader reader)
    {
        return new SysBranch
        {
            RowId = reader.GetDecimal(reader.GetOrdinal("ROW_ID")),
            ParRowId = reader.IsDBNull(reader.GetOrdinal("PAR_ROW_ID")) ? null : reader.GetDecimal(reader.GetOrdinal("PAR_ROW_ID")),
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
        return value == "Y" || value == "1";
    }
}
