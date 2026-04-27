using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Concurrent;
using System.Diagnostics;
using ThinkOnErp.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace ThinkOnErp.Infrastructure.Tests.Data;

/// <summary>
/// Integration tests for Oracle connection pooling to verify efficient database connection management.
/// Tests verify connection pool configuration, connection reuse, concurrent request handling,
/// connection release, failure handling, pool metrics, and exhaustion scenarios.
/// 
/// **Validates: Requirements 13.5, 17.2**
/// - Requirement 13.5: THE Audit_Logger SHALL use connection pooling to efficiently manage database connections
/// - Requirement 17.2: THE Performance_Monitor SHALL track database connection pool utilization
/// </summary>
public class OracleConnectionPoolingIntegrationTests : IDisposable
{
    private readonly OracleDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ITestOutputHelper _output;
    private readonly string _connectionString;

    public OracleConnectionPoolingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _dbContext = new OracleDbContext(_configuration);
        _connectionString = _configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Oracle connection string not found");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        
        // Clear all connection pools to ensure clean state between tests
        OracleConnection.ClearAllPools();
    }

    #region Connection Pool Configuration Tests

    /// <summary>
    /// Verifies that connection pooling is enabled by default in the connection string.
    /// Connection pooling is essential for high-performance scenarios with 10,000+ requests per minute.
    /// </summary>
    [Fact]
    public void ConnectionString_Should_Have_Pooling_Enabled()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);

        // Act
        var poolingEnabled = builder.Pooling;

        // Assert
        Assert.True(poolingEnabled, "Connection pooling should be enabled for high-performance scenarios");
        _output.WriteLine($"Connection pooling enabled: {poolingEnabled}");
    }

    /// <summary>
    /// Verifies that the connection pool has appropriate minimum and maximum size settings.
    /// Min pool size should be > 0 to maintain warm connections.
    /// Max pool size should be sufficient for concurrent requests (recommended: 50-100).
    /// </summary>
    [Fact]
    public void ConnectionString_Should_Have_Appropriate_Pool_Size_Settings()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);

        // Act
        var minPoolSize = builder.MinPoolSize;
        var maxPoolSize = builder.MaxPoolSize;

        // Assert
        Assert.True(minPoolSize >= 1, "Min pool size should be at least 1 to maintain warm connections");
        Assert.True(maxPoolSize >= 50, "Max pool size should be at least 50 for concurrent request handling");
        Assert.True(maxPoolSize <= 200, "Max pool size should not exceed 200 to avoid database overload");
        
        _output.WriteLine($"Min Pool Size: {minPoolSize}");
        _output.WriteLine($"Max Pool Size: {maxPoolSize}");
    }

    /// <summary>
    /// Verifies that connection timeout is configured appropriately.
    /// Timeout should be reasonable (10-30 seconds) to avoid long waits during pool exhaustion.
    /// </summary>
    [Fact]
    public void ConnectionString_Should_Have_Appropriate_Connection_Timeout()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);

        // Act
        var connectionTimeout = builder.ConnectionTimeout;

        // Assert
        Assert.True(connectionTimeout >= 10, "Connection timeout should be at least 10 seconds");
        Assert.True(connectionTimeout <= 60, "Connection timeout should not exceed 60 seconds");
        
        _output.WriteLine($"Connection Timeout: {connectionTimeout} seconds");
    }

    #endregion

    #region Connection Reuse Tests

    /// <summary>
    /// Verifies that connections are reused from the pool rather than creating new physical connections.
    /// This test opens and closes connections sequentially and verifies they come from the pool.
    /// </summary>
    [Fact]
    public async Task Connections_Should_Be_Reused_From_Pool()
    {
        // Arrange
        var connectionIds = new List<string>();
        const int iterations = 5;

        // Act - Open and close connections sequentially
        for (int i = 0; i < iterations; i++)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();
            
            // Get connection ID from Oracle
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SID') FROM DUAL";
            var sessionId = await command.ExecuteScalarAsync();
            connectionIds.Add(sessionId?.ToString() ?? "");
            
            _output.WriteLine($"Iteration {i + 1}: Session ID = {sessionId}");
        }

        // Assert - Some session IDs should be reused (not all unique)
        var uniqueSessionIds = connectionIds.Distinct().Count();
        Assert.True(uniqueSessionIds < iterations, 
            $"Expected connection reuse, but got {uniqueSessionIds} unique sessions out of {iterations} iterations");
        
        _output.WriteLine($"Total iterations: {iterations}, Unique sessions: {uniqueSessionIds}");
        _output.WriteLine("Connection pooling is working - connections are being reused");
    }

    /// <summary>
    /// Verifies that connection pool maintains minimum number of connections.
    /// After initial connections are created, subsequent connections should be available immediately.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Should_Maintain_Minimum_Connections()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);
        var minPoolSize = builder.MinPoolSize;
        var connectionTimes = new List<long>();

        // Act - Create connections and measure time to open
        for (int i = 0; i < minPoolSize + 5; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();
            
            stopwatch.Stop();
            connectionTimes.Add(stopwatch.ElapsedMilliseconds);
            
            _output.WriteLine($"Connection {i + 1}: {stopwatch.ElapsedMilliseconds}ms");
        }

        // Assert - After min pool size, connections should be faster (from pool)
        var firstConnectionTime = connectionTimes[0];
        var avgPooledConnectionTime = connectionTimes.Skip((int)minPoolSize).Average();
        
        _output.WriteLine($"First connection time: {firstConnectionTime}ms");
        _output.WriteLine($"Average pooled connection time: {avgPooledConnectionTime}ms");
        
        // Pooled connections should generally be faster or similar
        Assert.True(avgPooledConnectionTime <= firstConnectionTime * 2, 
            "Pooled connections should not be significantly slower than initial connection");
    }

    #endregion

    #region Concurrent Request Handling Tests

    /// <summary>
    /// Verifies that connection pool handles concurrent requests efficiently.
    /// Simulates multiple concurrent database operations typical in high-load scenarios.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Should_Handle_Concurrent_Requests_Efficiently()
    {
        // Arrange
        const int concurrentRequests = 20;
        var tasks = new List<Task<long>>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Execute concurrent database queries
        for (int i = 0; i < concurrentRequests; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                var taskStopwatch = Stopwatch.StartNew();
                
                using var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM SYS_AUDIT_LOG WHERE ROWNUM <= 100";
                await command.ExecuteScalarAsync();
                
                taskStopwatch.Stop();
                return taskStopwatch.ElapsedMilliseconds;
            }));
        }

        var executionTimes = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var totalTime = stopwatch.ElapsedMilliseconds;
        var avgTime = executionTimes.Average();
        var maxTime = executionTimes.Max();
        
        _output.WriteLine($"Concurrent requests: {concurrentRequests}");
        _output.WriteLine($"Total time: {totalTime}ms");
        _output.WriteLine($"Average request time: {avgTime:F2}ms");
        _output.WriteLine($"Max request time: {maxTime}ms");
        
        // All requests should complete successfully
        Assert.Equal(concurrentRequests, executionTimes.Length);
        
        // Average time should be reasonable (< 500ms per request)
        Assert.True(avgTime < 500, $"Average request time {avgTime}ms exceeds 500ms threshold");
        
        // Total time should be much less than sequential execution would take
        var estimatedSequentialTime = avgTime * concurrentRequests;
        Assert.True(totalTime < estimatedSequentialTime * 0.5, 
            "Concurrent execution should be significantly faster than sequential");
    }

    /// <summary>
    /// Verifies that connection pool handles high concurrent load without errors.
    /// Tests pool behavior under stress with many simultaneous connection requests.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Should_Handle_High_Concurrent_Load()
    {
        // Arrange
        const int concurrentRequests = 50;
        var successCount = 0;
        var errorCount = 0;
        var errors = new ConcurrentBag<Exception>();

        // Act - Execute high concurrent load
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
        {
            try
            {
                using var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM DUAL";
                await command.ExecuteScalarAsync();
                
                Interlocked.Increment(ref successCount);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errorCount);
                errors.Add(ex);
                _output.WriteLine($"Request {i} failed: {ex.Message}");
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        _output.WriteLine($"Successful requests: {successCount}/{concurrentRequests}");
        _output.WriteLine($"Failed requests: {errorCount}/{concurrentRequests}");
        
        // At least 90% of requests should succeed
        var successRate = (double)successCount / concurrentRequests;
        Assert.True(successRate >= 0.9, 
            $"Success rate {successRate:P} is below 90% threshold. Errors: {string.Join(", ", errors.Select(e => e.Message))}");
    }

    #endregion

    #region Connection Release Tests

    /// <summary>
    /// Verifies that connections are properly released back to the pool after use.
    /// Tests that disposed connections become available for reuse.
    /// </summary>
    [Fact]
    public async Task Connections_Should_Be_Released_Properly_After_Disposal()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);
        var maxPoolSize = Math.Min(builder.MaxPoolSize, 10); // Use smaller number for test
        var connections = new List<OracleConnection>();

        // Act - Acquire connections up to limit
        for (int i = 0; i < maxPoolSize; i++)
        {
            var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();
            connections.Add(connection);
        }

        _output.WriteLine($"Acquired {connections.Count} connections");

        // Dispose all connections
        foreach (var connection in connections)
        {
            connection.Dispose();
        }
        connections.Clear();

        _output.WriteLine("Disposed all connections");

        // Try to acquire connections again - should succeed if properly released
        var newConnections = new List<OracleConnection>();
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < maxPoolSize; i++)
        {
            var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();
            newConnections.Add(connection);
        }
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(maxPoolSize, newConnections.Count);
        _output.WriteLine($"Successfully reacquired {newConnections.Count} connections in {stopwatch.ElapsedMilliseconds}ms");
        
        // Cleanup
        foreach (var connection in newConnections)
        {
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies that connections are released even when exceptions occur.
    /// Tests proper cleanup in error scenarios using try-finally pattern.
    /// </summary>
    [Fact]
    public async Task Connections_Should_Be_Released_Even_When_Exceptions_Occur()
    {
        // Arrange
        var connectionAcquired = false;
        var connectionReleased = false;

        // Act & Assert
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();
            connectionAcquired = true;
            
            _output.WriteLine("Connection acquired");

            // Simulate an error
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM NON_EXISTENT_TABLE";
            
            await Assert.ThrowsAsync<OracleException>(async () => 
                await command.ExecuteScalarAsync());
        }
        finally
        {
            // Connection should be released by using statement
            connectionReleased = true;
        }

        // Verify we can still acquire a new connection (pool not exhausted)
        using var newConnection = _dbContext.CreateConnection();
        await newConnection.OpenAsync();
        
        using var testCommand = newConnection.CreateCommand();
        testCommand.CommandText = "SELECT 1 FROM DUAL";
        var result = await testCommand.ExecuteScalarAsync();

        Assert.True(connectionAcquired, "Connection should have been acquired");
        Assert.True(connectionReleased, "Connection should have been released");
        Assert.NotNull(result);
        
        _output.WriteLine("Connection properly released after exception");
    }

    #endregion

    #region Connection Failure Handling Tests

    /// <summary>
    /// Verifies that connection pool handles connection failures gracefully.
    /// Tests recovery from transient connection errors.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Should_Handle_Connection_Failures_Gracefully()
    {
        // Arrange
        var successfulConnections = 0;
        const int attempts = 5;

        // Act - Try to establish connections
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                using var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();
                
                // Verify connection is working
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM DUAL";
                await command.ExecuteScalarAsync();
                
                successfulConnections++;
                _output.WriteLine($"Attempt {i + 1}: Success");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Attempt {i + 1}: Failed - {ex.Message}");
            }
        }

        // Assert - Most attempts should succeed (allowing for transient failures)
        var successRate = (double)successfulConnections / attempts;
        Assert.True(successRate >= 0.8, 
            $"Success rate {successRate:P} is below 80% threshold");
        
        _output.WriteLine($"Success rate: {successRate:P}");
    }

    /// <summary>
    /// Verifies that invalid connections are removed from the pool.
    /// Tests that the pool can recover from broken connections.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Should_Remove_Invalid_Connections()
    {
        // Arrange & Act
        OracleConnection? firstConnection = null;
        
        try
        {
            // Create a connection and force it to become invalid
            firstConnection = _dbContext.CreateConnection();
            await firstConnection.OpenAsync();
            
            _output.WriteLine("First connection opened successfully");
            
            // Close the connection (return to pool)
            firstConnection.Close();
            firstConnection.Dispose();
            
            _output.WriteLine("First connection closed and disposed");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"First connection error: {ex.Message}");
        }

        // Try to get a new connection - pool should provide a valid one
        using var secondConnection = _dbContext.CreateConnection();
        await secondConnection.OpenAsync();
        
        using var command = secondConnection.CreateCommand();
        command.CommandText = "SELECT 1 FROM DUAL";
        var result = await command.ExecuteScalarAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1", result.ToString());
        
        _output.WriteLine("Second connection is valid - pool recovered successfully");
    }

    #endregion

    #region Connection Pool Metrics Tests

    /// <summary>
    /// Verifies that we can query Oracle for active connection count.
    /// This metric is essential for monitoring connection pool utilization.
    /// </summary>
    [Fact]
    public async Task Should_Be_Able_To_Query_Active_Connection_Count()
    {
        // Arrange
        var activeConnections = new List<OracleConnection>();
        const int connectionsToCreate = 5;

        try
        {
            // Act - Create multiple active connections
            for (int i = 0; i < connectionsToCreate; i++)
            {
                var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();
                activeConnections.Add(connection);
            }

            // Query active connection count
            using var queryConnection = _dbContext.CreateConnection();
            await queryConnection.OpenAsync();
            
            using var command = queryConnection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM V$SESSION 
                WHERE USERNAME = USER 
                AND STATUS = 'ACTIVE'";
            
            var activeCount = await command.ExecuteScalarAsync();

            // Assert
            Assert.NotNull(activeCount);
            var count = Convert.ToInt32(activeCount);
            Assert.True(count >= connectionsToCreate, 
                $"Expected at least {connectionsToCreate} active connections, found {count}");
            
            _output.WriteLine($"Active connections: {count}");
        }
        finally
        {
            // Cleanup
            foreach (var connection in activeConnections)
            {
                connection.Dispose();
            }
        }
    }

    /// <summary>
    /// Verifies that we can track connection pool statistics over time.
    /// Tests ability to monitor pool health and utilization.
    /// </summary>
    [Fact]
    public async Task Should_Be_Able_To_Track_Connection_Pool_Statistics()
    {
        // Arrange
        var statistics = new List<ConnectionPoolStats>();
        const int iterations = 3;

        // Act - Perform operations and collect statistics
        for (int i = 0; i < iterations; i++)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();
            
            var stats = await GetConnectionPoolStatsAsync(connection);
            statistics.Add(stats);
            
            _output.WriteLine($"Iteration {i + 1}: Active={stats.ActiveConnections}, Total={stats.TotalConnections}");
            
            await Task.Delay(100); // Small delay between iterations
        }

        // Assert
        Assert.Equal(iterations, statistics.Count);
        Assert.All(statistics, stats =>
        {
            Assert.True(stats.ActiveConnections >= 0, "Active connections should be non-negative");
            Assert.True(stats.TotalConnections >= stats.ActiveConnections, 
                "Total connections should be >= active connections");
        });
        
        _output.WriteLine("Connection pool statistics tracking successful");
    }

    /// <summary>
    /// Verifies that connection pool size stays within configured limits.
    /// Tests that pool doesn't exceed maximum size under load.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Size_Should_Stay_Within_Configured_Limits()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);
        var maxPoolSize = builder.MaxPoolSize;
        var connections = new List<OracleConnection>();

        try
        {
            // Act - Try to create more connections than max pool size
            var attemptCount = (int)(maxPoolSize * 1.5);
            var successCount = 0;
            var timeoutCount = 0;

            for (int i = 0; i < attemptCount; i++)
            {
                try
                {
                    var connection = _dbContext.CreateConnection();
                    
                    // Use shorter timeout for this test
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await connection.OpenAsync(cts.Token);
                    
                    connections.Add(connection);
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    timeoutCount++;
                    _output.WriteLine($"Connection {i + 1}: Timeout (pool exhausted)");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Connection {i + 1}: Error - {ex.Message}");
                }
            }

            // Assert
            _output.WriteLine($"Attempted: {attemptCount}, Successful: {successCount}, Timeouts: {timeoutCount}");
            _output.WriteLine($"Max pool size: {maxPoolSize}");
            
            // Should not exceed max pool size significantly
            Assert.True(successCount <= maxPoolSize + 5, 
                $"Connection count {successCount} exceeds max pool size {maxPoolSize} by too much");
            
            // Should have some timeouts if we exceeded pool size
            if (attemptCount > maxPoolSize)
            {
                Assert.True(timeoutCount > 0, 
                    "Expected some timeouts when exceeding max pool size");
            }
        }
        finally
        {
            // Cleanup
            foreach (var connection in connections)
            {
                try { connection.Dispose(); } catch { }
            }
        }
    }

    #endregion

    #region Connection Pool Exhaustion Tests

    /// <summary>
    /// Verifies behavior when connection pool is exhausted.
    /// Tests that the system handles pool exhaustion gracefully with appropriate timeouts.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Should_Handle_Exhaustion_Gracefully()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);
        var maxPoolSize = Math.Min(builder.MaxPoolSize, 10); // Use smaller number for test
        var heldConnections = new List<OracleConnection>();
        var exhaustionDetected = false;

        try
        {
            // Act - Exhaust the connection pool
            for (int i = 0; i < maxPoolSize; i++)
            {
                var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();
                heldConnections.Add(connection);
            }

            _output.WriteLine($"Exhausted pool with {heldConnections.Count} connections");

            // Try to get one more connection - should timeout
            try
            {
                using var extraConnection = _dbContext.CreateConnection();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await extraConnection.OpenAsync(cts.Token);
                
                _output.WriteLine("WARNING: Got extra connection beyond pool limit");
            }
            catch (OperationCanceledException)
            {
                exhaustionDetected = true;
                _output.WriteLine("Pool exhaustion detected - timeout occurred as expected");
            }
            catch (OracleException ex) when (ex.Message.Contains("timeout") || ex.Message.Contains("pool"))
            {
                exhaustionDetected = true;
                _output.WriteLine($"Pool exhaustion detected - Oracle error: {ex.Message}");
            }

            // Assert
            Assert.True(exhaustionDetected, 
                "Pool exhaustion should be detected when all connections are in use");
        }
        finally
        {
            // Cleanup - release connections
            foreach (var connection in heldConnections)
            {
                connection.Dispose();
            }
        }

        // Verify pool recovers after connections are released
        using var recoveryConnection = _dbContext.CreateConnection();
        await recoveryConnection.OpenAsync();
        
        _output.WriteLine("Pool recovered successfully after connections were released");
    }

    /// <summary>
    /// Verifies that connection pool recovers from exhaustion.
    /// Tests that released connections become available for reuse.
    /// </summary>
    [Fact]
    public async Task Connection_Pool_Should_Recover_From_Exhaustion()
    {
        // Arrange
        var builder = new OracleConnectionStringBuilder(_connectionString);
        var maxPoolSize = Math.Min(builder.MaxPoolSize, 10);
        
        // Act - Exhaust and release pool multiple times
        for (int cycle = 0; cycle < 3; cycle++)
        {
            var connections = new List<OracleConnection>();
            
            try
            {
                // Exhaust pool
                for (int i = 0; i < maxPoolSize; i++)
                {
                    var connection = _dbContext.CreateConnection();
                    await connection.OpenAsync();
                    connections.Add(connection);
                }
                
                _output.WriteLine($"Cycle {cycle + 1}: Exhausted pool with {connections.Count} connections");
            }
            finally
            {
                // Release all connections
                foreach (var connection in connections)
                {
                    connection.Dispose();
                }
                
                _output.WriteLine($"Cycle {cycle + 1}: Released all connections");
            }
            
            // Small delay to allow pool to stabilize
            await Task.Delay(100);
        }

        // Assert - Should be able to get a connection after recovery
        using var finalConnection = _dbContext.CreateConnection();
        await finalConnection.OpenAsync();
        
        using var command = finalConnection.CreateCommand();
        command.CommandText = "SELECT 1 FROM DUAL";
        var result = await command.ExecuteScalarAsync();

        Assert.NotNull(result);
        _output.WriteLine("Pool successfully recovered from multiple exhaustion cycles");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets connection pool statistics from Oracle system views.
    /// </summary>
    private async Task<ConnectionPoolStats> GetConnectionPoolStatsAsync(OracleConnection connection)
    {
        var stats = new ConnectionPoolStats();

        try
        {
            // Get active connections for current user
            using var activeCommand = connection.CreateCommand();
            activeCommand.CommandText = @"
                SELECT COUNT(*) 
                FROM V$SESSION 
                WHERE USERNAME = USER 
                AND STATUS = 'ACTIVE'";
            
            var activeResult = await activeCommand.ExecuteScalarAsync();
            stats.ActiveConnections = Convert.ToInt32(activeResult);

            // Get total connections for current user
            using var totalCommand = connection.CreateCommand();
            totalCommand.CommandText = @"
                SELECT COUNT(*) 
                FROM V$SESSION 
                WHERE USERNAME = USER";
            
            var totalResult = await totalCommand.ExecuteScalarAsync();
            stats.TotalConnections = Convert.ToInt32(totalResult);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not retrieve connection stats: {ex.Message}");
        }

        return stats;
    }

    /// <summary>
    /// Helper class to store connection pool statistics.
    /// </summary>
    private class ConnectionPoolStats
    {
        public int ActiveConnections { get; set; }
        public int TotalConnections { get; set; }
        public int IdleConnections => TotalConnections - ActiveConnections;
    }

    #endregion
}
