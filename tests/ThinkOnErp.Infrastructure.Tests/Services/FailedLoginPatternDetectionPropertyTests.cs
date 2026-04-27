using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for failed login pattern detection.
/// Validates that the SecurityMonitor correctly detects patterns of failed login attempts
/// and flags IP addresses as suspicious when the threshold is exceeded.
/// 
/// **Validates: Requirements 2.6, 10.1**
/// 
/// Property 5: Failed Login Pattern Detection
/// FOR ANY sequence of failed login attempts from the same IP address, when the count 
/// reaches the configured threshold within the time window, the Security_Monitor SHALL 
/// flag the IP address as suspicious.
/// </summary>
public class FailedLoginPatternDetectionPropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<OracleDbContext> _mockDbContext;
    private readonly Mock<ILogger<SecurityMonitor>> _mockLogger;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly SecurityMonitoringOptions _options;

    public FailedLoginPatternDetectionPropertyTests()
    {
        _mockDbContext = new Mock<OracleDbContext>();
        _mockLogger = new Mock<ILogger<SecurityMonitor>>();
        _mockCache = new Mock<IDistributedCache>();

        // Configure default options
        _options = new SecurityMonitoringOptions
        {
            Enabled = true,
            FailedLoginThreshold = 5,
            FailedLoginWindowMinutes = 5,
            UseRedisCache = true
        };
    }

    /// <summary>
    /// **Validates: Requirements 2.6, 10.1**
    /// 
    /// Property 5: Failed Login Pattern Detection
    /// 
    /// FOR ANY sequence of failed login attempts from the same IP address, when the count 
    /// reaches the configured threshold within the time window, the Security_Monitor SHALL 
    /// flag the IP address as suspicious.
    /// 
    /// This property verifies that:
    /// 1. When failed login attempts from an IP are below threshold, no threat is detected
    /// 2. When failed login attempts from an IP reach exactly the threshold, a threat is detected
    /// 3. When failed login attempts from an IP exceed the threshold, a threat is detected
    /// 4. The threat has correct ThreatType (FailedLoginPattern)
    /// 5. The threat has appropriate severity (High for threshold, Critical for 10+)
    /// 6. The threat contains the IP address and attempt count in metadata
    /// 7. Detection works correctly with Redis-based sliding window tracking
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAnyFailedLoginSequence_ThresholdExceeded_ThreatIsDetected(FailedLoginScenario scenario)
    {
        // Arrange: Setup Redis cache to return the specified number of failed attempts
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (_options.FailedLoginWindowMinutes * 60);

        // Create timestamps for failed login attempts (all within the time window)
        var timestamps = Enumerable.Range(0, scenario.FailedAttemptCount)
            .Select(i => windowStart + (i * 10)) // Spread attempts across the window
            .ToList();

        var cachedData = string.Join(',', timestamps);
        
        _mockCache
            .Setup(c => c.GetStringAsync(
                It.Is<string>(key => key == $"failed_logins:{scenario.IpAddress}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario.FailedAttemptCount > 0 ? cachedData : null);

        var optionsWrapper = Options.Create(_options);
        var securityMonitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            optionsWrapper,
            _mockCache.Object);

        // Act: Detect failed login pattern
        var threat = securityMonitor.DetectFailedLoginPatternAsync(scenario.IpAddress).GetAwaiter().GetResult();

        // Assert: Verify threat detection based on threshold
        var shouldDetectThreat = scenario.FailedAttemptCount >= _options.FailedLoginThreshold;
        var threatDetected = threat != null;

        if (!shouldDetectThreat)
        {
            // Property 1: Below threshold should not detect threat
            var result = !threatDetected;
            return result
                .Label($"Below threshold: {scenario.FailedAttemptCount} < {_options.FailedLoginThreshold}")
                .Label($"Threat detected: {threatDetected} (expected: false)")
                .Label($"IP: {scenario.IpAddress}");
        }

        // Property 2: At or above threshold should detect threat
        if (!threatDetected)
        {
            return false
                .Label($"At/above threshold: {scenario.FailedAttemptCount} >= {_options.FailedLoginThreshold}")
                .Label($"Threat detected: false (expected: true)")
                .Label($"IP: {scenario.IpAddress}");
        }

        // Property 3: Threat type must be FailedLoginPattern
        var correctThreatType = threat!.ThreatType == ThreatType.FailedLoginPattern;

        // Property 4: Severity must be appropriate (High for threshold, Critical for 10+)
        var expectedSeverity = scenario.FailedAttemptCount >= 10 
            ? ThreatSeverity.Critical 
            : ThreatSeverity.High;
        var correctSeverity = threat.Severity == expectedSeverity;

        // Property 5: IP address must be present in threat
        var ipAddressPresent = threat.IpAddress == scenario.IpAddress;

        // Property 6: Description must mention the IP and attempt count
        var descriptionContainsIp = threat.Description?.Contains(scenario.IpAddress) ?? false;
        var descriptionContainsCount = threat.Description?.Contains(scenario.FailedAttemptCount.ToString()) ?? false;

        // Property 7: Threat must be marked as active
        var isActive = threat.IsActive;

        // Property 8: DetectedAt timestamp must be recent (within last minute)
        var recentDetection = (DateTime.UtcNow - threat.DetectedAt).TotalMinutes < 1;

        // Property 9: Metadata must contain attempt count and threshold information
        var metadataValid = !string.IsNullOrEmpty(threat.Metadata);
        var metadataContainsAttempts = threat.Metadata?.Contains("FailedAttempts") ?? false;
        var metadataContainsThreshold = threat.Metadata?.Contains("Threshold") ?? false;

        // Combine all properties
        var allPropertiesValid = correctThreatType
            && correctSeverity
            && ipAddressPresent
            && descriptionContainsIp
            && descriptionContainsCount
            && isActive
            && recentDetection
            && metadataValid
            && metadataContainsAttempts
            && metadataContainsThreshold;

        return allPropertiesValid
            .Label($"Failed attempts: {scenario.FailedAttemptCount}")
            .Label($"Threshold: {_options.FailedLoginThreshold}")
            .Label($"Threat detected: {threatDetected}")
            .Label($"Correct threat type: {correctThreatType} (expected: FailedLoginPattern, actual: {threat.ThreatType})")
            .Label($"Correct severity: {correctSeverity} (expected: {expectedSeverity}, actual: {threat.Severity})")
            .Label($"IP address present: {ipAddressPresent} (expected: {scenario.IpAddress}, actual: {threat.IpAddress})")
            .Label($"Description contains IP: {descriptionContainsIp}")
            .Label($"Description contains count: {descriptionContainsCount}")
            .Label($"Is active: {isActive}")
            .Label($"Recent detection: {recentDetection}")
            .Label($"Metadata valid: {metadataValid}")
            .Label($"Metadata contains attempts: {metadataContainsAttempts}")
            .Label($"Metadata contains threshold: {metadataContainsThreshold}");
    }

    /// <summary>
    /// Property test to verify that failed login attempts outside the time window are not counted.
    /// This validates the sliding window behavior.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForFailedLoginsOutsideTimeWindow_NotCounted(FailedLoginWithOldAttemptsScenario scenario)
    {
        // Arrange: Setup Redis cache with both old and recent attempts
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (_options.FailedLoginWindowMinutes * 60);

        // Create old timestamps (outside window) and recent timestamps (inside window)
        var oldTimestamps = Enumerable.Range(0, scenario.OldAttemptCount)
            .Select(i => windowStart - 3600 - (i * 10)) // 1 hour before window start
            .ToList();

        var recentTimestamps = Enumerable.Range(0, scenario.RecentAttemptCount)
            .Select(i => windowStart + (i * 10))
            .ToList();

        var allTimestamps = oldTimestamps.Concat(recentTimestamps).ToList();
        var cachedData = string.Join(',', allTimestamps);

        _mockCache
            .Setup(c => c.GetStringAsync(
                It.Is<string>(key => key == $"failed_logins:{scenario.IpAddress}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedData);

        var optionsWrapper = Options.Create(_options);
        var securityMonitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            optionsWrapper,
            _mockCache.Object);

        // Act: Detect failed login pattern
        var threat = securityMonitor.DetectFailedLoginPatternAsync(scenario.IpAddress).GetAwaiter().GetResult();

        // Assert: Only recent attempts should be counted
        var shouldDetectThreat = scenario.RecentAttemptCount >= _options.FailedLoginThreshold;
        var threatDetected = threat != null;

        var result = shouldDetectThreat == threatDetected;

        return result
            .Label($"Old attempts (outside window): {scenario.OldAttemptCount}")
            .Label($"Recent attempts (inside window): {scenario.RecentAttemptCount}")
            .Label($"Threshold: {_options.FailedLoginThreshold}")
            .Label($"Should detect threat: {shouldDetectThreat}")
            .Label($"Threat detected: {threatDetected}")
            .Label($"IP: {scenario.IpAddress}");
    }

    /// <summary>
    /// Property test to verify that null or empty IP addresses are handled gracefully.
    /// </summary>
    [Fact]
    public async Task ForNullOrEmptyIpAddress_ReturnsNull()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var securityMonitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            optionsWrapper,
            _mockCache.Object);

        // Act & Assert
        var threat1 = await securityMonitor.DetectFailedLoginPatternAsync(null!);
        var threat2 = await securityMonitor.DetectFailedLoginPatternAsync("");
        var threat3 = await securityMonitor.DetectFailedLoginPatternAsync("   ");

        Assert.Null(threat1);
        Assert.Null(threat2);
        Assert.Null(threat3);
    }

    /// <summary>
    /// Property test to verify that exactly threshold attempts triggers detection.
    /// This is a boundary condition test.
    /// </summary>
    [Fact]
    public async Task ForExactlyThresholdAttempts_ThreatIsDetected()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (_options.FailedLoginWindowMinutes * 60);

        // Create exactly threshold number of attempts
        var timestamps = Enumerable.Range(0, _options.FailedLoginThreshold)
            .Select(i => windowStart + (i * 10))
            .ToList();

        var cachedData = string.Join(',', timestamps);

        _mockCache
            .Setup(c => c.GetStringAsync(
                It.Is<string>(key => key == $"failed_logins:{ipAddress}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedData);

        var optionsWrapper = Options.Create(_options);
        var securityMonitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            optionsWrapper,
            _mockCache.Object);

        // Act
        var threat = await securityMonitor.DetectFailedLoginPatternAsync(ipAddress);

        // Assert
        Assert.NotNull(threat);
        Assert.Equal(ThreatType.FailedLoginPattern, threat.ThreatType);
        Assert.Equal(ThreatSeverity.High, threat.Severity);
        Assert.Equal(ipAddress, threat.IpAddress);
    }

    /// <summary>
    /// Property test to verify that 10 or more attempts result in Critical severity.
    /// </summary>
    [Fact]
    public async Task ForTenOrMoreAttempts_SeverityIsCritical()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var attemptCount = 10;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (_options.FailedLoginWindowMinutes * 60);

        var timestamps = Enumerable.Range(0, attemptCount)
            .Select(i => windowStart + (i * 10))
            .ToList();

        var cachedData = string.Join(',', timestamps);

        _mockCache
            .Setup(c => c.GetStringAsync(
                It.Is<string>(key => key == $"failed_logins:{ipAddress}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedData);

        var optionsWrapper = Options.Create(_options);
        var securityMonitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            optionsWrapper,
            _mockCache.Object);

        // Act
        var threat = await securityMonitor.DetectFailedLoginPatternAsync(ipAddress);

        // Assert
        Assert.NotNull(threat);
        Assert.Equal(ThreatType.FailedLoginPattern, threat.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, threat.Severity);
        Assert.Equal(ipAddress, threat.IpAddress);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary failed login scenarios with varying attempt counts.
        /// Covers cases below, at, and above the threshold.
        /// </summary>
        public static Arbitrary<FailedLoginScenario> FailedLoginScenario()
        {
            var scenarioGenerator =
                from attemptCount in Gen.Choose(0, 20) // 0 to 20 attempts
                from ipAddress in Gen.Elements(
                    "192.168.1.100",
                    "10.0.0.50",
                    "172.16.0.25",
                    "203.0.113.45",
                    "198.51.100.10",
                    "2001:0db8:85a3:0000:0000:8a2e:0370:7334")
                select new FailedLoginScenario
                {
                    IpAddress = ipAddress,
                    FailedAttemptCount = attemptCount
                };

            return Arb.From(scenarioGenerator);
        }

        /// <summary>
        /// Generates scenarios with both old (outside window) and recent (inside window) attempts.
        /// Tests the sliding window behavior.
        /// </summary>
        public static Arbitrary<FailedLoginWithOldAttemptsScenario> FailedLoginWithOldAttemptsScenario()
        {
            var scenarioGenerator =
                from oldCount in Gen.Choose(0, 10) // 0 to 10 old attempts
                from recentCount in Gen.Choose(0, 10) // 0 to 10 recent attempts
                from ipAddress in Gen.Elements(
                    "192.168.1.100",
                    "10.0.0.50",
                    "172.16.0.25",
                    "203.0.113.45")
                select new FailedLoginWithOldAttemptsScenario
                {
                    IpAddress = ipAddress,
                    OldAttemptCount = oldCount,
                    RecentAttemptCount = recentCount
                };

            return Arb.From(scenarioGenerator);
        }
    }
}

/// <summary>
/// Represents a failed login scenario for property-based testing.
/// </summary>
public class FailedLoginScenario
{
    public string IpAddress { get; set; } = string.Empty;
    public int FailedAttemptCount { get; set; }
}

/// <summary>
/// Represents a failed login scenario with both old and recent attempts.
/// Tests sliding window behavior.
/// </summary>
public class FailedLoginWithOldAttemptsScenario
{
    public string IpAddress { get; set; } = string.Empty;
    public int OldAttemptCount { get; set; }
    public int RecentAttemptCount { get; set; }
}
