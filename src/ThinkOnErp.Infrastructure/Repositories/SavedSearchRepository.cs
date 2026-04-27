using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysSavedSearch entity using Oracle stored procedures.
/// Handles saved search CRUD operations following existing ThinkOnERP patterns.
/// Requirements: 8.6, 8.11, 19.9
/// </summary>
public class SavedSearchRepository : ISavedSearchRepository
{
    private readonly OracleDbContext _dbContext;

    public SavedSearchRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Creates a new saved search by calling SP_SYS_SAVED_SEARCH_INSERT.
    /// </summary>
    public async Task<Int64> CreateAsync(SysSavedSearch savedSearch)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SP_SYS_SAVED_SEARCH_INSERT";
        command.CommandType = CommandType.StoredProcedure;

        // Input parameters
        command.Parameters.Add("P_USER_ID", OracleDbType.Int64).Value = savedSearch.UserId;
        command.Parameters.Add("P_SEARCH_NAME", OracleDbType.NVarchar2).Value = savedSearch.SearchName;
        command.Parameters.Add("P_SEARCH_DESCRIPTION", OracleDbType.NVarchar2).Value = 
            (object?)savedSearch.SearchDescription ?? DBNull.Value;
        command.Parameters.Add("P_SEARCH_CRITERIA", OracleDbType.NClob).Value = savedSearch.SearchCriteria;
        command.Parameters.Add("P_IS_PUBLIC", OracleDbType.Char).Value = savedSearch.IsPublic ? "Y" : "N";
        command.Parameters.Add("P_IS_DEFAULT", OracleDbType.Char).Value = savedSearch.IsDefault ? "Y" : "N";
        command.Parameters.Add("P_CREATION_USER", OracleDbType.NVarchar2).Value = savedSearch.CreationUser;

        // Output parameter
        var newIdParam = command.Parameters.Add("P_NEW_ID", OracleDbType.Int64);
        newIdParam.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(newIdParam.Value.ToString());
    }

    /// <summary>
    /// Updates an existing saved search by calling SP_SYS_SAVED_SEARCH_UPDATE.
    /// </summary>
    public async Task<Int64> UpdateAsync(SysSavedSearch savedSearch)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SP_SYS_SAVED_SEARCH_UPDATE";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add("P_ROW_ID", OracleDbType.Int64).Value = savedSearch.RowId;
        command.Parameters.Add("P_SEARCH_NAME", OracleDbType.NVarchar2).Value = savedSearch.SearchName;
        command.Parameters.Add("P_SEARCH_DESCRIPTION", OracleDbType.NVarchar2).Value = 
            (object?)savedSearch.SearchDescription ?? DBNull.Value;
        command.Parameters.Add("P_SEARCH_CRITERIA", OracleDbType.NClob).Value = savedSearch.SearchCriteria;
        command.Parameters.Add("P_IS_PUBLIC", OracleDbType.Char).Value = savedSearch.IsPublic ? "Y" : "N";
        command.Parameters.Add("P_IS_DEFAULT", OracleDbType.Char).Value = savedSearch.IsDefault ? "Y" : "N";
        command.Parameters.Add("P_UPDATE_USER", OracleDbType.NVarchar2).Value = savedSearch.UpdateUser ?? savedSearch.CreationUser;

        await command.ExecuteNonQueryAsync();
        return savedSearch.RowId;
    }

    /// <summary>
    /// Retrieves all saved searches for a user by calling SP_SYS_SAVED_SEARCH_SELECT_BY_USER.
    /// </summary>
    public async Task<List<SysSavedSearch>> GetByUserIdAsync(Int64 userId)
    {
        var savedSearches = new List<SysSavedSearch>();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SP_SYS_SAVED_SEARCH_SELECT_BY_USER";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add("P_USER_ID", OracleDbType.Int64).Value = userId;

        var cursorParam = command.Parameters.Add("P_RESULT_CURSOR", OracleDbType.RefCursor);
        cursorParam.Direction = ParameterDirection.Output;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            savedSearches.Add(MapFromReader(reader));
        }

        return savedSearches;
    }

    /// <summary>
    /// Retrieves a specific saved search by ID by calling SP_SYS_SAVED_SEARCH_SELECT_BY_ID.
    /// </summary>
    public async Task<SysSavedSearch?> GetByIdAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SP_SYS_SAVED_SEARCH_SELECT_BY_ID";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add("P_ROW_ID", OracleDbType.Int64).Value = rowId;

        var cursorParam = command.Parameters.Add("P_RESULT_CURSOR", OracleDbType.RefCursor);
        cursorParam.Direction = ParameterDirection.Output;

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    /// <summary>
    /// Soft deletes a saved search by calling SP_SYS_SAVED_SEARCH_DELETE.
    /// </summary>
    public async Task<Int64> DeleteAsync(Int64 rowId, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SP_SYS_SAVED_SEARCH_DELETE";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add("P_ROW_ID", OracleDbType.Int64).Value = rowId;
        command.Parameters.Add("P_DELETE_USER", OracleDbType.NVarchar2).Value = userName;

        await command.ExecuteNonQueryAsync();
        return rowId;
    }

    /// <summary>
    /// Increments usage count by calling SP_SYS_SAVED_SEARCH_INCREMENT_USAGE.
    /// </summary>
    public async Task IncrementUsageAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SP_SYS_SAVED_SEARCH_INCREMENT_USAGE";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add("P_ROW_ID", OracleDbType.Int64).Value = rowId;

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Maps a data reader row to a SysSavedSearch entity.
    /// </summary>
    private SysSavedSearch MapFromReader(IDataReader reader)
    {
        return new SysSavedSearch
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            UserId = reader.GetInt64(reader.GetOrdinal("USER_ID")),
            SearchName = reader.GetString(reader.GetOrdinal("SEARCH_NAME")),
            SearchDescription = reader.IsDBNull(reader.GetOrdinal("SEARCH_DESCRIPTION")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("SEARCH_DESCRIPTION")),
            SearchCriteria = reader.GetString(reader.GetOrdinal("SEARCH_CRITERIA")),
            IsPublic = reader.GetString(reader.GetOrdinal("IS_PUBLIC")) == "Y",
            IsDefault = reader.GetString(reader.GetOrdinal("IS_DEFAULT")) == "Y",
            UsageCount = reader.GetInt32(reader.GetOrdinal("USAGE_COUNT")),
            LastUsedDate = reader.IsDBNull(reader.GetOrdinal("LAST_USED_DATE")) 
                ? null 
                : reader.GetDateTime(reader.GetOrdinal("LAST_USED_DATE")),
            IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "Y",
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) 
                ? null 
                : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) 
                ? null 
                : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }
}
