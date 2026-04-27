using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Security monitoring service that detects suspicious activities and security threats.
/// Implements threat detection algorithms for failed logins, unauthorized access, SQL injection, XSS, and anomalous activity.
/// Integrates with database for tracking patterns and persisting threats.
/// </summary>
public class SecurityMonitor : ISecurityMonitor
{
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<SecurityMonitor> _logger;
    private readonly SecurityMonitoringOptions _options;
    private readonly IDistributedCache? _cache;

    // SQL injection patterns to detect
    // Pattern 1: Classic SQL injection (UNION, SELECT, INSERT, UPDATE, DELETE, DROP)
    private static readonly Regex SqlInjectionPattern = new(
        @"(\bUNION\b.*\bSELECT\b)|(\bSELECT\b.*\bFROM\b)|(\bINSERT\b.*\bINTO\b)|" +
        @"(\bUPDATE\b.*\bSET\b)|(\bDELETE\b.*\bFROM\b)|(\bDROP\b.*\bTABLE\b)|" +
        @"(\bEXEC\b.*\()|(\bEXECUTE\b.*\()|(\bCAST\b.*\bAS\b)|" +
        @"(--)|(/\*)|(\*/)|(\bOR\b.*=.*)|(\bAND\b.*=.*)|" +
        @"(';)|('--)|('\s*OR\s*')|('\s*AND\s*')|(\bxp_)|(\bsp_)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Pattern 2: Time-based blind SQL injection (WAITFOR, SLEEP, BENCHMARK, pg_sleep)
    private static readonly Regex TimeBasedSqlInjectionPattern = new(
        @"(\bWAITFOR\b.*\bDELAY\b)|(\bSLEEP\b\s*\()|(\bBENCHMARK\b\s*\()|" +
        @"(\bpg_sleep\b\s*\()|(\bDBMS_LOCK\.SLEEP\b)|(\bGET_LOCK\b\s*\()",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Pattern 3: Boolean-based blind SQL injection
    private static readonly Regex BooleanBasedSqlInjectionPattern = new(
        @"(\bAND\b\s+\d+\s*=\s*\d+)|(\bOR\b\s+\d+\s*=\s*\d+)|" +
        @"(\bAND\b\s+'\w+'\s*=\s*'\w+')|(\bOR\b\s+'\w+'\s*=\s*'\w+')|" +
        @"(\bAND\b\s+\d+\s*<\s*\d+)|(\bOR\b\s+\d+\s*>\s*\d+)|" +
        @"(\bAND\b.*\bLIKE\b)|(\bOR\b.*\bLIKE\b)|" +
        @"(\bAND\b.*\bEXISTS\b)|(\bOR\b.*\bEXISTS\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Pattern 4: Encoded SQL injection attempts (hex, URL encoding)
    private static readonly Regex EncodedSqlInjectionPattern = new(
        @"(%27)|(%2527)|(%25%32%37)|(%20OR%20)|(%20AND%20)|" +
        @"(0x[0-9a-fA-F]+.*0x[0-9a-fA-F]+)|" +
        @"(CHAR\s*\(\s*\d+)|" +
        @"(CHR\s*\(\s*\d+)|" +
        @"(CONCAT\s*\(.*CHAR)|" +
        @"(\\x[0-9a-fA-F]{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Pattern 5: Stacked queries and command injection
    private static readonly Regex StackedQueryPattern = new(
        @"(;\s*SELECT\b)|(;\s*INSERT\b)|(;\s*UPDATE\b)|(;\s*DELETE\b)|(;\s*DROP\b)|(;\s*CREATE\b)|(;\s*ALTER\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Pattern 6: Information schema and system table access
    private static readonly Regex InformationSchemaPattern = new(
        @"(\binformation_schema\b)|(\bsys\.)|(\bsysobjects\b)|(\bsyscolumns\b)|" +
        @"(\ball_tables\b)|(\ball_tab_columns\b)|(\buser_tables\b)|" +
        @"(\bmysql\.user\b)|(\bpg_catalog\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // XSS patterns to detect
    private static readonly Regex XssPattern = new(
        @"(<script[^>]*>.*?</script>)|(<iframe[^>]*>)|(<object[^>]*>)|" +
        @"(<embed[^>]*>)|(<applet[^>]*>)|(<meta[^>]*>)|(<link[^>]*>)|" +
        @"(javascript:)|(onerror\s*=)|(onload\s*=)|(onclick\s*=)|" +
        @"(onmouseover\s*=)|(onfocus\s*=)|(onblur\s*=)|(eval\s*\()|" +
        @"(expression\s*\()|(<img[^>]*onerror)|(<body[^>]*onload)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public SecurityMonitor(
        OracleDbContext dbContext,
        ILogger<SecurityMonitor> logger,
        IOptions<SecurityMonitoringOptions> options,
        IDistributedCache? cache = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache;

        if (_options.UseRedisCache && _cache == null)
        {
            _logger.LogWarning(
                "UseRedisCache is enabled but IDistributedCache is not available. Falling back to database-only tracking.");
        }
    }

    /// <summary>
    /// Detect failed login patterns from a specific IP address.
    /// Uses Redis sliding window for distributed tracking when available, falls back to database.
    /// Checks if threshold or more failed login attempts occurred from the same IP within the configured time window.
    /// </summary>
    public async Task<SecurityThreat?> DetectFailedLoginPatternAsync(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            _logger.LogWarning("DetectFailedLoginPatternAsync called with null or empty IP address");
            return null;
        }

        try
        {
            int failedAttempts;

            // Use Redis sliding window if available and enabled
            if (_options.UseRedisCache && _cache != null)
            {
                failedAttempts = await DetectFailedLoginPatternWithRedisAsync(ipAddress);
            }
            else
            {
                // Fallback to database-based detection
                failedAttempts = await DetectFailedLoginPatternWithDatabaseAsync(ipAddress);
            }

            _logger.LogDebug(
                "Failed login check for IP {IpAddress}: {FailedAttempts} attempts in last {WindowMinutes} minutes",
                ipAddress, failedAttempts, _options.FailedLoginWindowMinutes);

            // Check if threshold exceeded
            if (failedAttempts >= _options.FailedLoginThreshold)
            {
                _logger.LogWarning(
                    "Failed login pattern detected for IP {IpAddress}: {FailedAttempts} attempts",
                    ipAddress, failedAttempts);

                var threat = new SecurityThreat
                {
                    ThreatType = ThreatType.FailedLoginPattern,
                    Severity = failedAttempts >= 10 ? ThreatSeverity.Critical : ThreatSeverity.High,
                    Description = $"Multiple failed login attempts detected: {failedAttempts} attempts from IP {ipAddress} in the last {_options.FailedLoginWindowMinutes} minutes",
                    IpAddress = ipAddress,
                    DetectedAt = DateTime.UtcNow,
                    IsActive = true,
                    CorrelationId = CorrelationContext.GetOrCreate(),
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        FailedAttempts = failedAttempts,
                        TimeWindowMinutes = _options.FailedLoginWindowMinutes,
                        Threshold = _options.FailedLoginThreshold,
                        DetectionMethod = _options.UseRedisCache && _cache != null ? "Redis" : "Database"
                    })
                };

                return threat;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting failed login pattern for IP {IpAddress}", ipAddress);
            return null;
        }
    }

    /// <summary>
    /// Detect failed login patterns using Redis sliding window algorithm.
    /// Uses sorted sets with timestamps as scores for efficient sliding window tracking.
    /// </summary>
    private async Task<int> DetectFailedLoginPatternWithRedisAsync(string ipAddress)
    {
        try
        {
            var cacheKey = $"failed_logins:{ipAddress}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var windowStart = now - (_options.FailedLoginWindowMinutes * 60);

            // Get all failed login timestamps from Redis
            var cachedData = await _cache!.GetStringAsync(cacheKey);
            
            if (string.IsNullOrEmpty(cachedData))
            {
                _logger.LogDebug("No cached failed login data found for IP {IpAddress}", ipAddress);
                return 0;
            }

            // Parse timestamps from cache (stored as comma-separated Unix timestamps)
            var timestamps = cachedData
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(long.Parse)
                .Where(ts => ts >= windowStart) // Filter to sliding window
                .ToList();

            _logger.LogDebug(
                "Redis sliding window for IP {IpAddress}: {Count} attempts in window (raw: {RawCount})",
                ipAddress, timestamps.Count, cachedData.Split(',', StringSplitOptions.RemoveEmptyEntries).Length);

            return timestamps.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting failed login pattern with Redis for IP {IpAddress}, falling back to database", ipAddress);
            // Fallback to database on Redis error
            return await DetectFailedLoginPatternWithDatabaseAsync(ipAddress);
        }
    }

    /// <summary>
    /// Detect failed login patterns using database query (fallback method).
    /// Queries SYS_FAILED_LOGINS table for attempts within the time window.
    /// </summary>
    private async Task<int> DetectFailedLoginPatternWithDatabaseAsync(string ipAddress)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        // Query failed login attempts from the configured time window
        var sql = @"
            SELECT COUNT(*) 
            FROM SYS_FAILED_LOGINS 
            WHERE IP_ADDRESS = :IpAddress 
            AND ATTEMPT_DATE >= SYSDATE - INTERVAL ':WindowMinutes' MINUTE";

        using var command = connection.CreateCommand();
        command.CommandText = sql.Replace(":WindowMinutes", _options.FailedLoginWindowMinutes.ToString());
        command.Parameters.Add(new OracleParameter("IpAddress", OracleDbType.NVarchar2) { Value = ipAddress });

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Track a failed login attempt in Redis (sliding window) and database.
    /// This method should be called by the authentication service when a login fails.
    /// </summary>
    public async Task TrackFailedLoginAttemptAsync(string ipAddress, string? username = null, string? failureReason = null)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Track in Redis if available
            if (_options.UseRedisCache && _cache != null)
            {
                await TrackFailedLoginInRedisAsync(ipAddress, now);
            }

            // Always track in database for audit trail
            await TrackFailedLoginInDatabaseAsync(ipAddress, username, failureReason);

            _logger.LogDebug(
                "Tracked failed login attempt for IP {IpAddress}, Username: {Username}",
                ipAddress, username ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking failed login attempt for IP {IpAddress}", ipAddress);
        }
    }

    /// <summary>
    /// Track failed login attempt in Redis using sliding window.
    /// Stores timestamps in a comma-separated list with automatic expiration.
    /// </summary>
    private async Task TrackFailedLoginInRedisAsync(string ipAddress, long timestamp)
    {
        try
        {
            var cacheKey = $"failed_logins:{ipAddress}";
            var windowStart = timestamp - (_options.FailedLoginWindowMinutes * 60);

            // Get existing timestamps
            var cachedData = await _cache!.GetStringAsync(cacheKey);
            var timestamps = new List<long>();

            if (!string.IsNullOrEmpty(cachedData))
            {
                // Parse and filter existing timestamps to sliding window
                timestamps = cachedData
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(long.Parse)
                    .Where(ts => ts >= windowStart)
                    .ToList();
            }

            // Add new timestamp
            timestamps.Add(timestamp);

            // Store back to Redis with expiration
            var newData = string.Join(',', timestamps);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.FailedLoginWindowMinutes * 2)
            };

            await _cache.SetStringAsync(cacheKey, newData, options);

            _logger.LogDebug(
                "Tracked failed login in Redis for IP {IpAddress}: {Count} attempts in window",
                ipAddress, timestamps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking failed login in Redis for IP {IpAddress}", ipAddress);
            // Continue execution - database tracking will still work
        }
    }

    /// <summary>
    /// Track failed login attempt in database for audit trail.
    /// </summary>
    private async Task TrackFailedLoginInDatabaseAsync(string ipAddress, string? username, string? failureReason)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO SYS_FAILED_LOGINS (
                ROW_ID, IP_ADDRESS, USERNAME, FAILURE_REASON, ATTEMPT_DATE
            ) VALUES (
                SEQ_SYS_FAILED_LOGIN.NEXTVAL, :IpAddress, :Username, :FailureReason, SYSDATE
            )";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new OracleParameter("IpAddress", OracleDbType.NVarchar2) { Value = ipAddress });
        command.Parameters.Add(new OracleParameter("Username", OracleDbType.NVarchar2) { Value = (object?)username ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("FailureReason", OracleDbType.NVarchar2) { Value = (object?)failureReason ?? DBNull.Value });

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Track a failed login attempt for a specific user (in addition to IP tracking).
    /// Supports per-user rate limiting across multiple IPs.
    /// </summary>
    public async Task<int> GetFailedLoginCountForUserAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return 0;
        }

        try
        {
            // Use Redis if available
            if (_options.UseRedisCache && _cache != null)
            {
                var cacheKey = $"failed_logins_user:{username}";
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var windowStart = now - (_options.FailedLoginWindowMinutes * 60);

                var cachedData = await _cache.GetStringAsync(cacheKey);
                
                if (string.IsNullOrEmpty(cachedData))
                {
                    return 0;
                }

                var timestamps = cachedData
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(long.Parse)
                    .Where(ts => ts >= windowStart)
                    .ToList();

                return timestamps.Count;
            }
            else
            {
                // Fallback to database
                using var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();

                var sql = @"
                    SELECT COUNT(*) 
                    FROM SYS_FAILED_LOGINS 
                    WHERE USERNAME = :Username 
                    AND ATTEMPT_DATE >= SYSDATE - INTERVAL ':WindowMinutes' MINUTE";

                using var command = connection.CreateCommand();
                command.CommandText = sql.Replace(":WindowMinutes", _options.FailedLoginWindowMinutes.ToString());
                command.Parameters.Add(new OracleParameter("Username", OracleDbType.NVarchar2) { Value = username });

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting failed login count for user {Username}", username);
            return 0;
        }
    }

    /// <summary>
    /// Detect unauthorized access attempts when a user tries to access data outside their assigned company or branch.
    /// </summary>
    public async Task<SecurityThreat?> DetectUnauthorizedAccessAsync(long userId, long companyId, long branchId)
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            // Check if user has access to the specified company and branch
            var sql = @"
                SELECT COUNT(*) 
                FROM SYS_USERS 
                WHERE ROW_ID = :UserId 
                AND COMPANY_ID = :CompanyId 
                AND (BRANCH_ID = :BranchId OR BRANCH_ID IS NULL)
                AND IS_ACTIVE = 1";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new OracleParameter("UserId", OracleDbType.Decimal) { Value = userId });
            command.Parameters.Add(new OracleParameter("CompanyId", OracleDbType.Decimal) { Value = companyId });
            command.Parameters.Add(new OracleParameter("BranchId", OracleDbType.Decimal) { Value = branchId });

            var result = await command.ExecuteScalarAsync();
            var hasAccess = Convert.ToInt32(result);

            if (hasAccess == 0)
            {
                _logger.LogWarning(
                    "Unauthorized access attempt detected: User {UserId} attempted to access Company {CompanyId}, Branch {BranchId}",
                    userId, companyId, branchId);

                // Get user details for better logging
                var userSql = "SELECT USERNAME FROM SYS_USERS WHERE ROW_ID = :UserId";
                using var userCommand = connection.CreateCommand();
                userCommand.CommandText = userSql;
                userCommand.Parameters.Add(new OracleParameter("UserId", OracleDbType.Decimal) { Value = userId });

                var usernameResult = await userCommand.ExecuteScalarAsync();
                var username = usernameResult?.ToString();

                var threat = new SecurityThreat
                {
                    ThreatType = ThreatType.UnauthorizedAccess,
                    Severity = ThreatSeverity.High,
                    Description = $"User {username ?? userId.ToString()} (ID: {userId}) attempted to access data outside their assigned company (ID: {companyId}) or branch (ID: {branchId})",
                    UserId = userId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    DetectedAt = DateTime.UtcNow,
                    IsActive = true,
                    CorrelationId = CorrelationContext.GetOrCreate(),
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Username = username,
                        AttemptedCompanyId = companyId,
                        AttemptedBranchId = branchId
                    })
                };

                return threat;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error detecting unauthorized access for User {UserId}, Company {CompanyId}, Branch {BranchId}",
                userId, companyId, branchId);
            return null;
        }
    }

    /// <summary>
    /// Detect SQL injection patterns in request parameters.
    /// Scans for classic SQL injection, time-based blind, boolean-based blind, encoded attempts, and more.
    /// Returns a SecurityThreat if SQL injection pattern is detected, null otherwise.
    /// </summary>
    public async Task<SecurityThreat?> DetectSqlInjectionAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        try
        {
            // Decode URL-encoded input for better detection
            var decodedInput = System.Net.WebUtility.UrlDecode(input);
            
            // Check against multiple SQL injection pattern categories
            var detectionResults = new List<(bool matched, string patternType, Match? match)>
            {
                (SqlInjectionPattern.IsMatch(input), "Classic SQL Injection", SqlInjectionPattern.Match(input)),
                (TimeBasedSqlInjectionPattern.IsMatch(input), "Time-Based Blind SQL Injection", TimeBasedSqlInjectionPattern.Match(input)),
                (BooleanBasedSqlInjectionPattern.IsMatch(input), "Boolean-Based Blind SQL Injection", BooleanBasedSqlInjectionPattern.Match(input)),
                (EncodedSqlInjectionPattern.IsMatch(input), "Encoded SQL Injection", EncodedSqlInjectionPattern.Match(input)),
                (StackedQueryPattern.IsMatch(input), "Stacked Query Injection", StackedQueryPattern.Match(input)),
                (InformationSchemaPattern.IsMatch(input), "Information Schema Access", InformationSchemaPattern.Match(input))
            };

            // Also check decoded input for encoded attacks
            if (decodedInput != input)
            {
                detectionResults.Add((SqlInjectionPattern.IsMatch(decodedInput), "Classic SQL Injection (Decoded)", SqlInjectionPattern.Match(decodedInput)));
                detectionResults.Add((TimeBasedSqlInjectionPattern.IsMatch(decodedInput), "Time-Based Blind SQL Injection (Decoded)", TimeBasedSqlInjectionPattern.Match(decodedInput)));
                detectionResults.Add((BooleanBasedSqlInjectionPattern.IsMatch(decodedInput), "Boolean-Based Blind SQL Injection (Decoded)", BooleanBasedSqlInjectionPattern.Match(decodedInput)));
            }

            // Find first matching pattern
            var detection = detectionResults.FirstOrDefault(r => r.matched);

            if (detection.matched && detection.match != null)
            {
                // Apply false positive filtering
                if (IsFalsePositive(input, detection.patternType))
                {
                    _logger.LogDebug(
                        "SQL injection pattern matched but classified as false positive. Pattern: {PatternType}, Input: {Input}",
                        detection.patternType, MaskSensitiveInput(input));
                    return null;
                }

                _logger.LogWarning(
                    "SQL injection pattern detected in input. Pattern Type: {PatternType}, Matched: {Pattern}",
                    detection.patternType, detection.match.Value);

                var threat = new SecurityThreat
                {
                    ThreatType = ThreatType.SqlInjection,
                    Severity = DetermineSqlInjectionSeverity(detection.patternType),
                    Description = $"SQL injection pattern detected in request input. Type: {detection.patternType}, Pattern: {detection.match.Value}",
                    DetectedAt = DateTime.UtcNow,
                    IsActive = true,
                    CorrelationId = CorrelationContext.GetOrCreate(),
                    TriggerData = MaskSensitiveInput(input),
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        PatternType = detection.patternType,
                        MatchedPattern = detection.match.Value,
                        InputLength = input.Length,
                        WasDecoded = decodedInput != input
                    })
                };

                return threat;
            }

            return null;
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "Regex timeout while checking for SQL injection patterns");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting SQL injection pattern");
            return null;
        }
    }

    /// <summary>
    /// Determine severity based on SQL injection pattern type.
    /// Time-based and stacked queries are most critical as they can cause significant damage.
    /// </summary>
    private ThreatSeverity DetermineSqlInjectionSeverity(string patternType)
    {
        return patternType switch
        {
            "Time-Based Blind SQL Injection" => ThreatSeverity.Critical,
            "Time-Based Blind SQL Injection (Decoded)" => ThreatSeverity.Critical,
            "Stacked Query Injection" => ThreatSeverity.Critical,
            "Classic SQL Injection" => ThreatSeverity.Critical,
            "Classic SQL Injection (Decoded)" => ThreatSeverity.Critical,
            "Information Schema Access" => ThreatSeverity.High,
            "Boolean-Based Blind SQL Injection" => ThreatSeverity.High,
            "Boolean-Based Blind SQL Injection (Decoded)" => ThreatSeverity.High,
            "Encoded SQL Injection" => ThreatSeverity.High,
            _ => ThreatSeverity.Critical
        };
    }

    /// <summary>
    /// Apply false positive filtering to reduce noise from legitimate business data.
    /// Checks for common false positive patterns like legitimate apostrophes in names.
    /// </summary>
    private bool IsFalsePositive(string input, string patternType)
    {
        // If input is very short and contains only simple apostrophe, likely a name
        if (input.Length < 30 && input.Count(c => c == '\'') == 1)
        {
            // Check if it's a simple name with apostrophe (e.g., "O'Brien", "John's")
            var simpleNamePattern = new Regex(@"^[\w\s]*'[\w\s]*$", RegexOptions.None, TimeSpan.FromMilliseconds(50));
            if (simpleNamePattern.IsMatch(input))
            {
                return true;
            }
        }

        // If pattern is boolean-based and input looks like a legitimate comparison
        if (patternType.Contains("Boolean-Based"))
        {
            // Check if it's a legitimate business logic comparison (no quotes or SQL keywords nearby)
            if (!input.Contains("'") && !input.Contains("\"") && 
                !input.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !input.Contains("FROM", StringComparison.OrdinalIgnoreCase))
            {
                // Might be legitimate business logic like "status=1 AND active=1"
                // But this is still suspicious in user input, so don't filter it
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Detect cross-site scripting (XSS) patterns in request parameters.
    /// </summary>
    public async Task<SecurityThreat?> DetectXssAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        try
        {
            // Check if input matches XSS patterns
            var match = XssPattern.Match(input);

            if (match.Success)
            {
                _logger.LogWarning(
                    "XSS pattern detected in input. Matched pattern: {Pattern}",
                    match.Value);

                var threat = new SecurityThreat
                {
                    ThreatType = ThreatType.XssAttempt,
                    Severity = ThreatSeverity.High,
                    Description = $"Cross-site scripting (XSS) pattern detected in request input: {match.Value}",
                    DetectedAt = DateTime.UtcNow,
                    IsActive = true,
                    CorrelationId = CorrelationContext.GetOrCreate(),
                    TriggerData = MaskSensitiveInput(input),
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        MatchedPattern = match.Value,
                        InputLength = input.Length
                    })
                };

                return threat;
            }

            return null;
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "Regex timeout while checking for XSS patterns");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting XSS pattern");
            return null;
        }
    }

    /// <summary>
    /// Detect anomalous activity for a specific user.
    /// Checks for unusual patterns such as unusually high API request volumes or requests at unusual times.
    /// </summary>
    public async Task<SecurityThreat?> DetectAnomalousActivityAsync(long userId)
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            // Check for unusually high request volume in the last hour
            var requestVolumeSql = @"
                SELECT COUNT(*) 
                FROM SYS_AUDIT_LOG 
                WHERE ACTOR_ID = :UserId 
                AND CREATION_DATE >= SYSDATE - INTERVAL '1' HOUR
                AND EVENT_CATEGORY = 'Request'";

            using var command = connection.CreateCommand();
            command.CommandText = requestVolumeSql;
            command.Parameters.Add(new OracleParameter("UserId", OracleDbType.Decimal) { Value = userId });

            var result = await command.ExecuteScalarAsync();
            var requestCount = Convert.ToInt32(result);

            _logger.LogDebug(
                "Anomalous activity check for User {UserId}: {RequestCount} requests in last hour",
                userId, requestCount);

            // Check if request volume exceeds threshold
            if (requestCount >= _options.AnomalousActivityThreshold)
            {
                _logger.LogWarning(
                    "Anomalous activity detected for User {UserId}: {RequestCount} requests in last hour",
                    userId, requestCount);

                // Get user details
                var userSql = "SELECT USERNAME FROM SYS_USERS WHERE ROW_ID = :UserId";
                using var userCommand = connection.CreateCommand();
                userCommand.CommandText = userSql;
                userCommand.Parameters.Add(new OracleParameter("UserId", OracleDbType.Decimal) { Value = userId });

                var usernameResult = await userCommand.ExecuteScalarAsync();
                var username = usernameResult?.ToString();

                var threat = new SecurityThreat
                {
                    ThreatType = ThreatType.AnomalousActivity,
                    Severity = requestCount >= _options.AnomalousActivityThreshold * 2 
                        ? ThreatSeverity.Critical 
                        : ThreatSeverity.Medium,
                    Description = $"Anomalous activity detected for user {username ?? userId.ToString()} (ID: {userId}): {requestCount} requests in the last hour (threshold: {_options.AnomalousActivityThreshold})",
                    UserId = userId,
                    DetectedAt = DateTime.UtcNow,
                    IsActive = true,
                    CorrelationId = CorrelationContext.GetOrCreate(),
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Username = username,
                        RequestCount = requestCount,
                        TimeWindowHours = 1,
                        Threshold = _options.AnomalousActivityThreshold
                    })
                };

                return threat;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting anomalous activity for User {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Trigger a security alert for a detected threat.
    /// Persists the threat to the database.
    /// </summary>
    public async Task TriggerSecurityAlertAsync(SecurityThreat threat)
    {
        if (threat == null)
        {
            throw new ArgumentNullException(nameof(threat));
        }

        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            // Insert threat into database
            var sql = @"
                INSERT INTO SYS_SECURITY_THREATS (
                    ROW_ID, THREAT_TYPE, SEVERITY, IP_ADDRESS, USER_ID, COMPANY_ID,
                    DESCRIPTION, DETECTION_DATE, STATUS, METADATA
                ) VALUES (
                    SEQ_SYS_SECURITY_THREAT.NEXTVAL, :ThreatType, :Severity, :IpAddress, :UserId, :CompanyId,
                    :Description, :DetectionDate, 'Active', :Metadata
                )";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new OracleParameter("ThreatType", OracleDbType.NVarchar2) { Value = threat.ThreatType.ToString() });
            command.Parameters.Add(new OracleParameter("Severity", OracleDbType.NVarchar2) { Value = threat.Severity.ToString() });
            command.Parameters.Add(new OracleParameter("IpAddress", OracleDbType.NVarchar2) { Value = (object?)threat.IpAddress ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("UserId", OracleDbType.Decimal) { Value = (object?)threat.UserId ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("CompanyId", OracleDbType.Decimal) { Value = (object?)threat.CompanyId ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("Description", OracleDbType.NVarchar2) { Value = threat.Description });
            command.Parameters.Add(new OracleParameter("DetectionDate", OracleDbType.Date) { Value = threat.DetectedAt });
            command.Parameters.Add(new OracleParameter("Metadata", OracleDbType.Clob) { Value = (object?)threat.Metadata ?? DBNull.Value });

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                _logger.LogInformation(
                    "Security threat persisted: Type={ThreatType}, Severity={Severity}, Description={Description}",
                    threat.ThreatType, threat.Severity, threat.Description);
            }
            else
            {
                _logger.LogWarning("Failed to persist security threat to database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering security alert for threat: {ThreatType}", threat.ThreatType);
            throw;
        }
    }

    /// <summary>
    /// Get all active security threats that have not been resolved.
    /// Results are ordered by severity (Critical first) and detection time (newest first).
    /// </summary>
    public async Task<PagedResult<SecurityThreat>> GetActiveThreatsAsync(PaginationOptions pagination)
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            // First, get the total count
            var countSql = @"
                SELECT COUNT(*)
                FROM SYS_SECURITY_THREATS
                WHERE STATUS = 'Active'";

            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = countSql;
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            // Then get the paged results
            var sql = @"
                SELECT * FROM (
                    SELECT 
                        ROW_ID,
                        THREAT_TYPE,
                        SEVERITY,
                        DESCRIPTION,
                        IP_ADDRESS,
                        USER_ID,
                        COMPANY_ID,
                        DETECTION_DATE,
                        STATUS,
                        METADATA,
                        ROW_NUMBER() OVER (
                            ORDER BY 
                                CASE SEVERITY
                                    WHEN 'Critical' THEN 1
                                    WHEN 'High' THEN 2
                                    WHEN 'Medium' THEN 3
                                    WHEN 'Low' THEN 4
                                END,
                                DETECTION_DATE DESC
                        ) AS RN
                    FROM SYS_SECURITY_THREATS
                    WHERE STATUS = 'Active'
                )
                WHERE RN > :Offset AND RN <= :OffsetPlusPageSize";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var offsetParam = command.CreateParameter();
            offsetParam.ParameterName = "Offset";
            offsetParam.Value = pagination.Skip;
            command.Parameters.Add(offsetParam);

            var offsetPlusPageSizeParam = command.CreateParameter();
            offsetPlusPageSizeParam.ParameterName = "OffsetPlusPageSize";
            offsetPlusPageSizeParam.Value = pagination.Skip + pagination.PageSize;
            command.Parameters.Add(offsetPlusPageSizeParam);

            var threats = new List<SecurityThreat>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var threat = new SecurityThreat
                {
                    Id = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
                    ThreatType = Enum.Parse<ThreatType>(reader.GetString(reader.GetOrdinal("THREAT_TYPE"))),
                    Severity = Enum.Parse<ThreatSeverity>(reader.GetString(reader.GetOrdinal("SEVERITY"))),
                    Description = reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                    IpAddress = reader.IsDBNull(reader.GetOrdinal("IP_ADDRESS")) ? null : reader.GetString(reader.GetOrdinal("IP_ADDRESS")),
                    UserId = reader.IsDBNull(reader.GetOrdinal("USER_ID")) ? null : reader.GetInt64(reader.GetOrdinal("USER_ID")),
                    CompanyId = reader.IsDBNull(reader.GetOrdinal("COMPANY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
                    DetectedAt = reader.GetDateTime(reader.GetOrdinal("DETECTION_DATE")),
                    IsActive = reader.GetString(reader.GetOrdinal("STATUS")) == "Active",
                    Metadata = reader.IsDBNull(reader.GetOrdinal("METADATA")) ? null : reader.GetString(reader.GetOrdinal("METADATA"))
                };

                threats.Add(threat);
            }

            return new PagedResult<SecurityThreat>
            {
                Items = threats,
                TotalCount = totalCount,
                Page = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active security threats");
            return new PagedResult<SecurityThreat>
            {
                Items = new List<SecurityThreat>(),
                TotalCount = 0,
                Page = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }
    }

    /// <summary>
    /// Generate a daily security summary report for administrators.
    /// </summary>
    public async Task<SecuritySummaryReport> GenerateDailySummaryAsync(DateTime date)
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            // Initialize report
            var report = new SecuritySummaryReport
            {
                ReportDate = date,
                GeneratedAt = DateTime.UtcNow
            };

            // Get total threat count and counts by severity
            var severitySql = @"
                SELECT 
                    SEVERITY,
                    COUNT(*) as CNT
                FROM SYS_SECURITY_THREATS
                WHERE DETECTION_DATE >= :StartDate AND DETECTION_DATE < :EndDate
                GROUP BY SEVERITY";

            using var severityCommand = connection.CreateCommand();
            severityCommand.CommandText = severitySql;
            severityCommand.Parameters.Add(new OracleParameter("StartDate", OracleDbType.Date) { Value = startDate });
            severityCommand.Parameters.Add(new OracleParameter("EndDate", OracleDbType.Date) { Value = endDate });

            using var severityReader = await severityCommand.ExecuteReaderAsync();
            while (await severityReader.ReadAsync())
            {
                var severity = severityReader.GetString(0);
                var count = Convert.ToInt32(severityReader.GetDecimal(1));

                report.TotalThreatsDetected += count;

                switch (severity)
                {
                    case "Critical":
                        report.CriticalThreats = count;
                        break;
                    case "High":
                        report.HighThreats = count;
                        break;
                    case "Medium":
                        report.MediumThreats = count;
                        break;
                    case "Low":
                        report.LowThreats = count;
                        break;
                }
            }

            // Get failed login count
            var failedLoginSql = @"
                SELECT COUNT(*) 
                FROM SYS_FAILED_LOGINS
                WHERE ATTEMPT_DATE >= :StartDate AND ATTEMPT_DATE < :EndDate";

            using var failedLoginCommand = connection.CreateCommand();
            failedLoginCommand.CommandText = failedLoginSql;
            failedLoginCommand.Parameters.Add(new OracleParameter("StartDate", OracleDbType.Date) { Value = startDate });
            failedLoginCommand.Parameters.Add(new OracleParameter("EndDate", OracleDbType.Date) { Value = endDate });

            var failedLoginResult = await failedLoginCommand.ExecuteScalarAsync();
            report.TotalFailedLogins = Convert.ToInt32(failedLoginResult);

            // Get unique suspicious IPs
            var suspiciousIpSql = @"
                SELECT COUNT(DISTINCT IP_ADDRESS)
                FROM SYS_SECURITY_THREATS
                WHERE DETECTION_DATE >= :StartDate AND DETECTION_DATE < :EndDate
                AND IP_ADDRESS IS NOT NULL";

            using var suspiciousIpCommand = connection.CreateCommand();
            suspiciousIpCommand.CommandText = suspiciousIpSql;
            suspiciousIpCommand.Parameters.Add(new OracleParameter("StartDate", OracleDbType.Date) { Value = startDate });
            suspiciousIpCommand.Parameters.Add(new OracleParameter("EndDate", OracleDbType.Date) { Value = endDate });

            var suspiciousIpResult = await suspiciousIpCommand.ExecuteScalarAsync();
            report.SuspiciousIpAddresses = Convert.ToInt32(suspiciousIpResult);

            // Get threat counts by type
            var typeSql = @"
                SELECT 
                    THREAT_TYPE,
                    COUNT(*) as CNT
                FROM SYS_SECURITY_THREATS
                WHERE DETECTION_DATE >= :StartDate AND DETECTION_DATE < :EndDate
                GROUP BY THREAT_TYPE";

            using var typeCommand = connection.CreateCommand();
            typeCommand.CommandText = typeSql;
            typeCommand.Parameters.Add(new OracleParameter("StartDate", OracleDbType.Date) { Value = startDate });
            typeCommand.Parameters.Add(new OracleParameter("EndDate", OracleDbType.Date) { Value = endDate });

            using var typeReader = await typeCommand.ExecuteReaderAsync();
            while (await typeReader.ReadAsync())
            {
                var threatTypeStr = typeReader.GetString(0);
                var count = Convert.ToInt32(typeReader.GetDecimal(1));

                if (Enum.TryParse<ThreatType>(threatTypeStr, out var threatType))
                {
                    report.ThreatsByType[threatType] = count;
                }
            }

            // Populate specific threat type counts
            report.UnauthorizedAccessAttempts = report.ThreatsByType.GetValueOrDefault(ThreatType.UnauthorizedAccess, 0);
            report.SqlInjectionAttempts = report.ThreatsByType.GetValueOrDefault(ThreatType.SqlInjection, 0);
            report.XssAttempts = report.ThreatsByType.GetValueOrDefault(ThreatType.XssAttempt, 0);
            report.AnomalousActivityUsers = report.ThreatsByType.GetValueOrDefault(ThreatType.AnomalousActivity, 0);

            // Get resolved and active threat counts
            var statusSql = @"
                SELECT 
                    STATUS,
                    COUNT(*) as CNT
                FROM SYS_SECURITY_THREATS
                WHERE DETECTION_DATE >= :StartDate AND DETECTION_DATE < :EndDate
                GROUP BY STATUS";

            using var statusCommand = connection.CreateCommand();
            statusCommand.CommandText = statusSql;
            statusCommand.Parameters.Add(new OracleParameter("StartDate", OracleDbType.Date) { Value = startDate });
            statusCommand.Parameters.Add(new OracleParameter("EndDate", OracleDbType.Date) { Value = endDate });

            using var statusReader = await statusCommand.ExecuteReaderAsync();
            while (await statusReader.ReadAsync())
            {
                var status = statusReader.GetString(0);
                var count = Convert.ToInt32(statusReader.GetDecimal(1));

                if (status == "Resolved")
                {
                    report.ResolvedThreats = count;
                }
                else if (status == "Active")
                {
                    report.ActiveThreats = count;
                }
            }

            // Note: Top IPs and Top Users lists are left empty for now
            // These would require more complex queries with ADO.NET
            // TODO: Implement top IPs and top users queries

            _logger.LogInformation(
                "Generated daily security summary for {Date}: {TotalThreats} threats detected",
                date.ToShortDateString(), report.TotalThreatsDetected);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily security summary for {Date}", date);
            throw;
        }
    }

    /// <summary>
    /// Mask sensitive input data for logging purposes.
    /// Shows first and last 10 characters, masks the middle.
    /// </summary>
    private string MaskSensitiveInput(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= 20)
        {
            return "***MASKED***";
        }

        var start = input.Substring(0, 10);
        var end = input.Substring(input.Length - 10);
        return $"{start}...{end} (length: {input.Length})";
    }
}
