using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Wrapper for OracleConnection that intercepts command execution for audit logging.
/// Automatically logs INSERT, UPDATE, DELETE operations to the audit trail.
/// </summary>
public class AuditableOracleConnection : IDbConnection
{
    private readonly OracleConnection _innerConnection;
    private readonly AuditCommandInterceptor _interceptor;

    public AuditableOracleConnection(OracleConnection innerConnection, AuditCommandInterceptor interceptor)
    {
        _innerConnection = innerConnection ?? throw new ArgumentNullException(nameof(innerConnection));
        _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
    }

    public string ConnectionString
    {
        get => _innerConnection.ConnectionString;
        set => _innerConnection.ConnectionString = value;
    }

    public int ConnectionTimeout => _innerConnection.ConnectionTimeout;

    public string Database => _innerConnection.Database;

    public ConnectionState State => _innerConnection.State;

    public IDbTransaction BeginTransaction()
    {
        return _innerConnection.BeginTransaction();
    }

    public IDbTransaction BeginTransaction(IsolationLevel il)
    {
        return _innerConnection.BeginTransaction(il);
    }

    public void ChangeDatabase(string databaseName)
    {
        _innerConnection.ChangeDatabase(databaseName);
    }

    public void Close()
    {
        _innerConnection.Close();
    }

    public IDbCommand CreateCommand()
    {
        var command = _innerConnection.CreateCommand();
        return new AuditableOracleCommand((OracleCommand)command, _interceptor);
    }

    public void Dispose()
    {
        _innerConnection.Dispose();
    }

    public void Open()
    {
        _innerConnection.Open();
    }

    /// <summary>
    /// Gets the underlying Oracle connection for direct access when needed.
    /// </summary>
    public OracleConnection GetInnerConnection()
    {
        return _innerConnection;
    }
}

/// <summary>
/// Wrapper for OracleCommand that intercepts execution for audit logging.
/// </summary>
public class AuditableOracleCommand : IDbCommand
{
    private readonly OracleCommand _innerCommand;
    private readonly AuditCommandInterceptor _interceptor;

    public AuditableOracleCommand(OracleCommand innerCommand, AuditCommandInterceptor interceptor)
    {
        _innerCommand = innerCommand ?? throw new ArgumentNullException(nameof(innerCommand));
        _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
    }

    public string CommandText
    {
        get => _innerCommand.CommandText;
        set => _innerCommand.CommandText = value;
    }

    public int CommandTimeout
    {
        get => _innerCommand.CommandTimeout;
        set => _innerCommand.CommandTimeout = value;
    }

    public CommandType CommandType
    {
        get => _innerCommand.CommandType;
        set => _innerCommand.CommandType = value;
    }

    public IDbConnection? Connection
    {
        get => _innerCommand.Connection != null ? new AuditableOracleConnection(_innerCommand.Connection, _interceptor) : null;
        set
        {
            if (value is AuditableOracleConnection auditableConn)
            {
                _innerCommand.Connection = auditableConn.GetInnerConnection();
            }
            else if (value is OracleConnection oracleConn)
            {
                _innerCommand.Connection = oracleConn;
            }
        }
    }

    public IDataParameterCollection Parameters => _innerCommand.Parameters;

    public IDbTransaction? Transaction
    {
        get => _innerCommand.Transaction;
        set => _innerCommand.Transaction = value as OracleTransaction;
    }

    public UpdateRowSource UpdatedRowSource
    {
        get => _innerCommand.UpdatedRowSource;
        set => _innerCommand.UpdatedRowSource = value;
    }

    public void Cancel()
    {
        _innerCommand.Cancel();
    }

    public IDbDataParameter CreateParameter()
    {
        return _innerCommand.CreateParameter();
    }

    public void Dispose()
    {
        _innerCommand.Dispose();
    }

    public int ExecuteNonQuery()
    {
        var rowsAffected = _innerCommand.ExecuteNonQuery();
        
        // Fire and forget audit logging (don't block the operation)
        _ = Task.Run(async () =>
        {
            try
            {
                await _interceptor.OnCommandExecutedAsync(_innerCommand, rowsAffected);
            }
            catch
            {
                // Swallow exceptions to prevent breaking the database operation
            }
        });

        return rowsAffected;
    }

    public IDataReader ExecuteReader()
    {
        return _innerCommand.ExecuteReader();
    }

    public IDataReader ExecuteReader(CommandBehavior behavior)
    {
        return _innerCommand.ExecuteReader(behavior);
    }

    public object? ExecuteScalar()
    {
        return _innerCommand.ExecuteScalar();
    }

    public void Prepare()
    {
        _innerCommand.Prepare();
    }

    /// <summary>
    /// Gets the underlying Oracle command for direct access when needed.
    /// </summary>
    public OracleCommand GetInnerCommand()
    {
        return _innerCommand;
    }
}
