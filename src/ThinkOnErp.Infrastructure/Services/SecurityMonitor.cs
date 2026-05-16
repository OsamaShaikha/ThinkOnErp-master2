using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;

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
        var cutoff = DateTime.UtcNow.AddMinutes(-_options.FailedLoginWindowMinutes);
        return await _dbContext.SysFailedLogins
            .CountAsync(f => f.IpAddress == ipAddress && f.AttemptDate >= cutoff);
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
        var entity = new SysFailedLogin
        {
            IpAddress = ipAddress,
            Username = username,
            FailureReason = failureReason,
            AttemptDate = DateTime.UtcNow
        };
        _dbContext.SysFailedLogins.Add(entity);
        await _dbContext.SaveChangesAsync();
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
                var cutoff = DateTime.UtcNow.AddMinutes(-_options.FailedLoginWindowMinutes);
                return await _dbContext.SysFailedLogins
                    .CountAsync(f => f.Username == username && f.AttemptDate >= cutoff);
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
            // Check if user has access to the specified company and branch
            var hasAccess = await _dbContext.SysUsers
                .CountAsync(u => u.Id == userId
                    && u.CompanyId == companyId
                    && (u.BranchId == branchId || u.BranchId == null)
                    && u.IsActive);

            if (hasAccess == 0)
            {
                _logger.LogWarning(
                    "Unauthorized access attempt detected: User {UserId} attempted to access Company {CompanyId}, Branch {BranchId}",
                    userId, companyId, branchId);

                // Get user details for better logging
                var username = await _dbContext.SysUsers
                    .Where(u => u.Id == userId)
                    .Select(u => u.UserName)
                    .FirstOrDefaultAsync();

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
            var cutoff = DateTime.UtcNow.AddHours(-1);

            // Check for unusually high request volume in the last hour
            var requestCount = await _dbContext.SysAuditLogs
                .CountAsync(a => a.ActorId == userId
                    && a.CreationDate >= cutoff
                    && a.EventCategory == "Request");

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
                var username = await _dbContext.SysUsers
                    .Where(u => u.Id == userId)
                    .Select(u => u.UserName)
                    .FirstOrDefaultAsync();

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
            var entity = new SysSecurityThreat
            {
                ThreatType = threat.ThreatType.ToString(),
                Severity = threat.Severity.ToString(),
                IpAddress = threat.IpAddress,
                UserId = threat.UserId,
                CompanyId = threat.CompanyId,
                Description = threat.Description,
                DetectionDate = threat.DetectedAt,
                Status = "Active",
                Metadata = threat.Metadata
            };

            _dbContext.SysSecurityThreats.Add(entity);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Security threat persisted: Type={ThreatType}, Severity={Severity}, Description={Description}",
                threat.ThreatType, threat.Severity, threat.Description);
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
            // First, get the total count
            var totalCount = await _dbContext.SysSecurityThreats
                .CountAsync(t => t.Status == "Active");

            // Then get the paged results
            var entities = await _dbContext.SysSecurityThreats
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.Severity == "Critical" ? 1
                    : t.Severity == "High" ? 2
                    : t.Severity == "Medium" ? 3
                    : 4)
                .ThenByDescending(t => t.DetectionDate)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToListAsync();

            var threats = entities.Select(e => new SecurityThreat
            {
                Id = e.Id,
                ThreatType = Enum.Parse<ThreatType>(e.ThreatType),
                Severity = Enum.Parse<ThreatSeverity>(e.Severity),
                Description = e.Description,
                IpAddress = e.IpAddress,
                UserId = e.UserId,
                CompanyId = e.CompanyId,
                DetectedAt = e.DetectionDate,
                IsActive = e.Status == "Active",
                Metadata = e.Metadata
            }).ToList();

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
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            // Initialize report
            var report = new SecuritySummaryReport
            {
                ReportDate = date,
                GeneratedAt = DateTime.UtcNow
            };

            // Get counts by severity
            var severityGroups = await _dbContext.SysSecurityThreats
                .Where(t => t.DetectionDate >= startDate && t.DetectionDate < endDate)
                .GroupBy(t => t.Severity)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var group in severityGroups)
            {
                report.TotalThreatsDetected += group.Count;

                switch (group.Severity)
                {
                    case "Critical":
                        report.CriticalThreats = group.Count;
                        break;
                    case "High":
                        report.HighThreats = group.Count;
                        break;
                    case "Medium":
                        report.MediumThreats = group.Count;
                        break;
                    case "Low":
                        report.LowThreats = group.Count;
                        break;
                }
            }

            // Get failed login count
            report.TotalFailedLogins = await _dbContext.SysFailedLogins
                .CountAsync(f => f.AttemptDate >= startDate && f.AttemptDate < endDate);

            // Get unique suspicious IPs
            report.SuspiciousIpAddresses = await _dbContext.SysSecurityThreats
                .Where(t => t.DetectionDate >= startDate && t.DetectionDate < endDate && t.IpAddress != null)
                .Select(t => t.IpAddress)
                .Distinct()
                .CountAsync();

            // Get threat counts by type
            var typeGroups = await _dbContext.SysSecurityThreats
                .Where(t => t.DetectionDate >= startDate && t.DetectionDate < endDate)
                .GroupBy(t => t.ThreatType)
                .Select(g => new { ThreatType = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var group in typeGroups)
            {
                if (Enum.TryParse<ThreatType>(group.ThreatType, out var threatType))
                {
                    report.ThreatsByType[threatType] = group.Count;
                }
            }

            // Populate specific threat type counts
            report.UnauthorizedAccessAttempts = report.ThreatsByType.GetValueOrDefault(ThreatType.UnauthorizedAccess, 0);
            report.SqlInjectionAttempts = report.ThreatsByType.GetValueOrDefault(ThreatType.SqlInjection, 0);
            report.XssAttempts = report.ThreatsByType.GetValueOrDefault(ThreatType.XssAttempt, 0);
            report.AnomalousActivityUsers = report.ThreatsByType.GetValueOrDefault(ThreatType.AnomalousActivity, 0);

            // Get resolved and active threat counts
            var statusGroups = await _dbContext.SysSecurityThreats
                .Where(t => t.DetectionDate >= startDate && t.DetectionDate < endDate)
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var group in statusGroups)
            {
                if (group.Status == "Resolved")
                {
                    report.ResolvedThreats = group.Count;
                }
                else if (group.Status == "Active")
                {
                    report.ActiveThreats = group.Count;
                }
            }

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
