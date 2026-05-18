using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Implementation of legacy audit service for backward compatibility.
/// Provides audit data in the exact format shown in logs.png interface.
/// Transforms comprehensive audit data into business-friendly format with status management.
/// </summary>
public class LegacyAuditService : ILegacyAuditService
{
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<LegacyAuditService> _logger;

    // Device type mappings for User-Agent parsing
    private static readonly Dictionary<string, string> DeviceTypeMappings = new()
    {
        { "pos", "POS Terminal" },
        { "mobile", "Mobile Device" },
        { "tablet", "Tablet" },
        { "desktop", "Desktop" },
        { "kiosk", "Kiosk" },
        { "chrome", "Desktop" },
        { "firefox", "Desktop" },
        { "safari", "Desktop" },
        { "edge", "Desktop" },
        { "android", "Mobile Device" },
        { "iphone", "Mobile Device" },
        { "ipad", "Tablet" }
    };

    // Business module mappings
    private static readonly Dictionary<string, string> ModuleMappings = new()
    {
        // Entity type mappings
        { "ticket", "Support" },
        { "user", "HR" },
        { "company", "Administration" },
        { "branch", "Administration" },
        { "role", "Security" },
        { "permission", "Security" },
        { "currency", "Accounting" },
        { "fiscalyear", "Accounting" },
        { "invoice", "Accounting" },
        { "payment", "Accounting" },
        { "product", "Inventory" },
        { "sale", "POS" },
        { "customer", "CRM" },
        { "supplier", "Procurement" },
        
        // Endpoint path mappings
        { "/api/auth", "Security" },
        { "/api/users", "HR" },
        { "/api/companies", "Administration" },
        { "/api/branches", "Administration" },
        { "/api/roles", "Security" },
        { "/api/permissions", "Security" },
        { "/api/currencies", "Accounting" },
        { "/api/tickets", "Support" },
        { "/api/pos", "POS" },
        { "/api/inventory", "Inventory" },
        { "/api/accounting", "Accounting" },
        { "/api/crm", "CRM" },
        { "/api/hr", "HR" }
    };

    // Error code prefixes by exception type
    private static readonly Dictionary<string, string> ErrorCodePrefixes = new()
    {
        { "OracleException", "DB" },
        { "TimeoutException", "TIMEOUT" },
        { "UnauthorizedAccessException", "AUTH" },
        { "ValidationException", "VALIDATION" },
        { "ArgumentException", "ARGUMENT" },
        { "InvalidOperationException", "OPERATION" },
        { "NotSupportedException", "NOTSUPPORTED" },
        { "NotImplementedException", "NOTIMPL" },
        { "HttpRequestException", "HTTP" },
        { "TaskCanceledException", "CANCELLED" },
        { "OutOfMemoryException", "MEMORY" },
        { "StackOverflowException", "STACK" },
        { "AccessViolationException", "ACCESS" },
        { "DivideByZeroException", "MATH" },
        { "IndexOutOfRangeException", "INDEX" },
        { "NullReferenceException", "NULL" },
        { "FileNotFoundException", "FILE" },
        { "DirectoryNotFoundException", "DIRECTORY" },
        { "IOException", "IO" },
        { "SecurityException", "SECURITY" },
        { "CryptographicException", "CRYPTO" }
    };

    public LegacyAuditService(OracleDbContext dbContext, ILogger<LegacyAuditService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<LegacyAuditLogDto>> GetLegacyAuditLogsAsync(
        LegacyAuditLogFilter filter, 
        PaginationOptions pagination)
    {
        try
        {
            var query = _dbContext.SysAuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(a => a.CompanyId.HasValue && _dbContext.SysCompanies.Any(c => c.Id == a.CompanyId.Value && c.CompanyNameEn!.Contains(filter.Company)));
            if (!string.IsNullOrWhiteSpace(filter.Module))
                query = query.Where(a => a.BusinessModule != null && a.BusinessModule.Contains(filter.Module));
            if (!string.IsNullOrWhiteSpace(filter.Branch))
                query = query.Where(a => a.BranchId.HasValue && _dbContext.SysBranches.Any(b => b.Id == a.BranchId.Value && b.BranchNameEn!.Contains(filter.Branch)));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(a => a.Status == filter.Status);
            if (filter.StartDate.HasValue)
                query = query.Where(a => a.CreationDate >= filter.StartDate.Value);
            if (filter.EndDate.HasValue)
                query = query.Where(a => a.CreationDate <= filter.EndDate.Value);
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                query = query.Where(a => a.Action.Contains(filter.SearchTerm) || a.EntityType.Contains(filter.SearchTerm) || (a.BusinessDescription != null && a.BusinessDescription.Contains(filter.SearchTerm)));

            var totalCount = await query.CountAsync();

            var skip = (pagination.PageNumber - 1) * pagination.PageSize;
            var auditLogs = await query
                .OrderByDescending(a => a.CreationDate)
                .Skip(skip)
                .Take(pagination.PageSize)
                .ToListAsync();

            var items = new List<LegacyAuditLogDto>();
            foreach (var log in auditLogs)
            {
                items.Add(await TransformToLegacyFormatAsync(MapToAuditLogEntry(log)));
            }

            return new PagedResult<LegacyAuditLogDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve legacy audit logs with filter: {@Filter}", filter);
            throw;
        }
    }

    private AuditLogEntry MapToAuditLogEntry(SysAuditLog log)
    {
        var entry = new AuditLogEntry
        {
            Id = log.Id,
            CorrelationId = log.CorrelationId,
            ActorType = log.ActorType,
            ActorId = log.ActorId,
            CompanyId = log.CompanyId,
            BranchId = log.BranchId,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            HttpMethod = log.HttpMethod,
            EndpointPath = log.EndpointPath,
            RequestPayload = log.RequestPayload,
            ResponsePayload = log.ResponsePayload,
            ExecutionTimeMs = log.ExecutionTimeMs,
            StatusCode = log.StatusCode,
            ExceptionType = log.ExceptionType,
            ExceptionMessage = log.ExceptionMessage,
            StackTrace = log.StackTrace,
            Severity = log.Severity ?? "Info",
            EventCategory = log.EventCategory ?? "DataChange",
            Metadata = log.Metadata,
            CreationDate = log.CreationDate,
            BusinessModule = log.BusinessModule,
            BusinessDescription = log.BusinessDescription,
            DeviceIdentifier = log.DeviceIdentifier,
            ErrorCode = log.ErrorCode,
            CompanyName = null,
            BranchName = null,
            ActorName = null
        };

        // Resolve company and branch names from the database
        try
        {
            if (log.CompanyId.HasValue)
            {
                var company = _dbContext.SysCompanies
                    .Where(c => c.Id == log.CompanyId.Value)
                    .Select(c => c.CompanyNameEn ?? c.CompanyNameAr)
                    .FirstOrDefault();
                entry.CompanyName = company;
            }

            if (log.BranchId.HasValue)
            {
                var branch = _dbContext.SysBranches
                    .Where(b => b.Id == log.BranchId.Value)
                    .Select(b => b.BranchNameEn ?? b.BranchNameAr)
                    .FirstOrDefault();
                entry.BranchName = branch;
            }

            if (log.ActorId > 0)
            {
                if (string.Equals(log.ActorType, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ActorName = _dbContext.SysSuperAdmins
                        .Where(u => u.Id == log.ActorId)
                        .Select(u => u.NameEn ?? u.NameAr ?? u.UserName)
                        .FirstOrDefault();
                }
                else
                {
                    entry.ActorName = _dbContext.SysUsers
                        .Where(u => u.Id == log.ActorId)
                        .Select(u => u.FullNameEn ?? u.FullNameAr ?? u.UserName)
                        .FirstOrDefault();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve company/branch/user names for audit log {AuditLogId}", log.Id);
        }

        return entry;
    }

    /// <inheritdoc/>
    public async Task<LegacyDashboardCounters> GetLegacyDashboardCountersAsync()
    {
        try
        {
            var allLogs = _dbContext.SysAuditLogs;
            var unresolvedCount = await allLogs.CountAsync(a => a.Status == null || a.Status == "Unresolved");
            var inProgressCount = await allLogs.CountAsync(a => a.Status == "InProgress");
            var resolvedCount = await allLogs.CountAsync(a => a.Status == "Resolved");
            var criticalCount = await allLogs.CountAsync(a => a.Severity == "Critical" || a.Severity == "High");

            return new LegacyDashboardCounters
            {
                UnresolvedCount = unresolvedCount,
                InProgressCount = inProgressCount,
                ResolvedCount = resolvedCount,
                CriticalErrorsCount = criticalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve legacy dashboard counters");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(long auditLogId, string status, string? resolutionNotes = null, long? assignedToUserId = null)
    {
        try
        {
            var auditLog = await _dbContext.SysAuditLogs.FindAsync(auditLogId);
            if (auditLog == null)
            {
                _logger.LogWarning("Audit log not found: {AuditLogId}", auditLogId);
                return;
            }

            auditLog.Status = status;
            if (!string.IsNullOrWhiteSpace(resolutionNotes))
                auditLog.Metadata = resolutionNotes;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated audit log {AuditLogId} status to {Status}", auditLogId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status for audit log {AuditLogId}", auditLogId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetCurrentStatusAsync(long auditLogId)
    {
        try
        {
            var auditLog = await _dbContext.SysAuditLogs
                .Where(a => a.Id == auditLogId)
                .Select(a => a.Status)
                .FirstOrDefaultAsync();
            return auditLog ?? "Unresolved";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current status for audit log {AuditLogId}", auditLogId);
            return "Unresolved";
        }
    }

    /// <inheritdoc/>
    public async Task<LegacyAuditLogDto> TransformToLegacyFormatAsync(AuditLogEntry auditEntry)
    {
        try
        {
            return new LegacyAuditLogDto
            {
                Id = auditEntry.Id,
                ErrorDescription = await GenerateBusinessDescriptionAsync(auditEntry),
                Module = await DetermineBusinessModuleAsync(auditEntry.EntityType, auditEntry.EndpointPath),
                Company = auditEntry.CompanyName ?? "Unknown",
                Branch = auditEntry.BranchName ?? "Unknown",
                User = auditEntry.ActorName ?? "System",
                Device = await ExtractDeviceIdentifierAsync(auditEntry.UserAgent ?? "", auditEntry.IpAddress),
                DateTime = auditEntry.CreationDate,
                Status = await GetCurrentStatusAsync(auditEntry.Id),
                ErrorCode = await GenerateErrorCodeAsync(auditEntry.ExceptionType ?? "Unknown", auditEntry.EntityType),
                CorrelationId = auditEntry.CorrelationId,
                CanResolve = true,
                CanDelete = false,
                CanViewDetails = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transform audit entry {AuditLogId} to legacy format", auditEntry.Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateBusinessDescriptionAsync(AuditLogEntry auditEntry)
    {
        try
        {
            // If we already have a business description, use it
            if (!string.IsNullOrEmpty(auditEntry.BusinessDescription))
            {
                return auditEntry.BusinessDescription;
            }

            // Generate business-friendly description based on audit entry data
            string description;
            switch (auditEntry.Action.ToUpperInvariant())
            {
                case "INSERT":
                    description = GenerateInsertDescription(auditEntry);
                    break;
                case "UPDATE":
                    description = GenerateUpdateDescription(auditEntry);
                    break;
                case "DELETE":
                    description = GenerateDeleteDescription(auditEntry);
                    break;
                case "LOGIN":
                    description = GenerateLoginDescription(auditEntry);
                    break;
                case "LOGOUT":
                    description = GenerateLogoutDescription(auditEntry);
                    break;
                case "EXCEPTION":
                    description = await GenerateExceptionDescription(auditEntry);
                    break;
                case "AUTHORIZATION_FAILURE":
                    description = GenerateAuthorizationFailureDescription(auditEntry);
                    break;
                case "STATUS_CHANGE":
                    description = GenerateStatusChangeDescription(auditEntry);
                    break;
                case "ASSIGNMENT_CHANGE":
                    description = GenerateAssignmentChangeDescription(auditEntry);
                    break;
                case "COMMENT_ADDED":
                    description = GenerateCommentDescription(auditEntry);
                    break;
                case "ATTACHMENT_UPLOADED":
                    description = GenerateAttachmentUploadDescription(auditEntry);
                    break;
                case "ATTACHMENT_DOWNLOADED":
                    description = GenerateAttachmentDownloadDescription(auditEntry);
                    break;
                case "SEARCH":
                    description = GenerateSearchDescription(auditEntry);
                    break;
                case "VIEW":
                    description = GenerateViewDescription(auditEntry);
                    break;
                default:
                    description = GenerateGenericDescription(auditEntry);
                    break;
            }

            return description;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate business description for audit entry {AuditLogId}", auditEntry.Id);
            return "System activity occurred";
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExtractDeviceIdentifierAsync(string userAgent, string? ipAddress)
    {
        try
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return GenerateIpBasedDeviceIdentifier(ipAddress);
            }

            var userAgentLower = userAgent.ToLowerInvariant();
            
            // Check for POS terminal patterns (highest priority)
            var posIdentifier = ExtractPosTerminalIdentifier(userAgent, userAgentLower, ipAddress);
            if (!string.IsNullOrEmpty(posIdentifier))
            {
                return posIdentifier;
            }

            // Check for kiosk patterns
            var kioskIdentifier = ExtractKioskIdentifier(userAgent, userAgentLower, ipAddress);
            if (!string.IsNullOrEmpty(kioskIdentifier))
            {
                return kioskIdentifier;
            }

            // Check for mobile devices
            var mobileIdentifier = ExtractMobileDeviceIdentifier(userAgent, userAgentLower);
            if (!string.IsNullOrEmpty(mobileIdentifier))
            {
                return mobileIdentifier;
            }

            // Check for tablets
            var tabletIdentifier = ExtractTabletIdentifier(userAgent, userAgentLower);
            if (!string.IsNullOrEmpty(tabletIdentifier))
            {
                return tabletIdentifier;
            }

            // Check for desktop browsers with department context
            var desktopIdentifier = ExtractDesktopIdentifier(userAgent, userAgentLower, ipAddress);
            if (!string.IsNullOrEmpty(desktopIdentifier))
            {
                return desktopIdentifier;
            }

            // Check for specialized applications
            var appIdentifier = ExtractApplicationIdentifier(userAgent, userAgentLower);
            if (!string.IsNullOrEmpty(appIdentifier))
            {
                return appIdentifier;
            }

            // Fallback to IP-based identification
            return GenerateIpBasedDeviceIdentifier(ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract device identifier from User-Agent: {UserAgent}", userAgent);
            return GenerateIpBasedDeviceIdentifier(ipAddress);
        }
    }

    private string? ExtractPosTerminalIdentifier(string userAgent, string userAgentLower, string? ipAddress)
    {
        // Check for explicit POS terminal patterns
        if (userAgentLower.Contains("pos") || userAgentLower.Contains("terminal"))
        {
            // Try to extract terminal number from various patterns
            var patterns = new[]
            {
                @"pos[^\d]*(\d+)",           // POS 03, POS-03, POS_03
                @"terminal[^\d]*(\d+)",      // Terminal 03, Terminal-03
                @"station[^\d]*(\d+)",       // Station 03
                @"register[^\d]*(\d+)"       // Register 03
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(userAgent, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var terminalNumber = match.Groups[1].Value;
                    return $"POS Terminal {terminalNumber.PadLeft(2, '0')}";
                }
            }

            // If no number found, try to extract from IP address
            if (!string.IsNullOrEmpty(ipAddress))
            {
                var ipMatch = Regex.Match(ipAddress, @"\.(\d+)$");
                if (ipMatch.Success)
                {
                    var lastOctet = ipMatch.Groups[1].Value;
                    return $"POS Terminal {lastOctet.PadLeft(2, '0')}";
                }
            }

            return "POS Terminal";
        }

        // Check for IP patterns that suggest POS terminals (e.g., 192.168.100.x range)
        if (!string.IsNullOrEmpty(ipAddress))
        {
            var posIpPatterns = new[]
            {
                @"192\.168\.100\.(\d+)",     // POS subnet
                @"10\.0\.100\.(\d+)",        // Alternative POS subnet
                @"172\.16\.100\.(\d+)"       // Another POS subnet
            };

            foreach (var pattern in posIpPatterns)
            {
                var match = Regex.Match(ipAddress, pattern);
                if (match.Success)
                {
                    var deviceNumber = match.Groups[1].Value;
                    return $"POS Terminal {deviceNumber.PadLeft(2, '0')}";
                }
            }
        }

        return null;
    }

    private string? ExtractKioskIdentifier(string userAgent, string userAgentLower, string? ipAddress)
    {
        if (userAgentLower.Contains("kiosk") || userAgentLower.Contains("self-service"))
        {
            // Try to extract kiosk number
            var kioskMatch = Regex.Match(userAgent, @"kiosk[^\d]*(\d+)|self[^\d]*service[^\d]*(\d+)", RegexOptions.IgnoreCase);
            if (kioskMatch.Success)
            {
                var kioskNumber = kioskMatch.Groups[1].Value ?? kioskMatch.Groups[2].Value;
                return $"Kiosk {kioskNumber.PadLeft(2, '0')}";
            }

            // Try to extract from IP if available
            if (!string.IsNullOrEmpty(ipAddress))
            {
                var ipMatch = Regex.Match(ipAddress, @"\.(\d+)$");
                if (ipMatch.Success)
                {
                    var lastOctet = ipMatch.Groups[1].Value;
                    return $"Kiosk {lastOctet.PadLeft(2, '0')}";
                }
            }

            return "Kiosk";
        }

        return null;
    }

    private string? ExtractMobileDeviceIdentifier(string userAgent, string userAgentLower)
    {
        // Android devices
        if (userAgentLower.Contains("android"))
        {
            // Try to extract Android version
            var androidMatch = Regex.Match(userAgent, @"Android\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (androidMatch.Success)
            {
                var version = androidMatch.Groups[1].Value;
                return $"Android {version}";
            }
            return "Android Mobile";
        }

        // iPhone
        if (userAgentLower.Contains("iphone"))
        {
            // Try to extract iOS version
            var iosMatch = Regex.Match(userAgent, @"OS\s+(\d+(?:_\d+)?)", RegexOptions.IgnoreCase);
            if (iosMatch.Success)
            {
                var version = iosMatch.Groups[1].Value.Replace("_", ".");
                return $"iPhone iOS {version}";
            }
            return "iPhone";
        }

        // Windows Phone
        if (userAgentLower.Contains("windows phone"))
        {
            return "Windows Phone";
        }

        // Generic mobile detection
        if (userAgentLower.Contains("mobile"))
        {
            return "Mobile Device";
        }

        return null;
    }

    private string? ExtractTabletIdentifier(string userAgent, string userAgentLower)
    {
        // iPad
        if (userAgentLower.Contains("ipad"))
        {
            // Try to extract iOS version
            var iosMatch = Regex.Match(userAgent, @"OS\s+(\d+(?:_\d+)?)", RegexOptions.IgnoreCase);
            if (iosMatch.Success)
            {
                var version = iosMatch.Groups[1].Value.Replace("_", ".");
                return $"iPad iOS {version}";
            }
            return "iPad";
        }

        // Android tablets
        if (userAgentLower.Contains("tablet") && userAgentLower.Contains("android"))
        {
            var androidMatch = Regex.Match(userAgent, @"Android\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (androidMatch.Success)
            {
                var version = androidMatch.Groups[1].Value;
                return $"Android Tablet {version}";
            }
            return "Android Tablet";
        }

        // Generic tablet
        if (userAgentLower.Contains("tablet"))
        {
            return "Tablet";
        }

        return null;
    }

    private string? ExtractDesktopIdentifier(string userAgent, string userAgentLower, string? ipAddress)
    {
        string? browserInfo = null;
        string? departmentInfo = null;

        // Extract browser information
        if (userAgentLower.Contains("chrome") && !userAgentLower.Contains("edge"))
        {
            var chromeMatch = Regex.Match(userAgent, @"Chrome/(\d+)", RegexOptions.IgnoreCase);
            if (chromeMatch.Success)
            {
                browserInfo = $"Chrome {chromeMatch.Groups[1].Value}";
            }
            else
            {
                browserInfo = "Chrome";
            }
        }
        else if (userAgentLower.Contains("firefox"))
        {
            var firefoxMatch = Regex.Match(userAgent, @"Firefox/(\d+)", RegexOptions.IgnoreCase);
            if (firefoxMatch.Success)
            {
                browserInfo = $"Firefox {firefoxMatch.Groups[1].Value}";
            }
            else
            {
                browserInfo = "Firefox";
            }
        }
        else if (userAgentLower.Contains("edge"))
        {
            var edgeMatch = Regex.Match(userAgent, @"Edg/(\d+)", RegexOptions.IgnoreCase);
            if (edgeMatch.Success)
            {
                browserInfo = $"Edge {edgeMatch.Groups[1].Value}";
            }
            else
            {
                browserInfo = "Edge";
            }
        }
        else if (userAgentLower.Contains("safari") && !userAgentLower.Contains("chrome"))
        {
            browserInfo = "Safari";
        }
        else if (userAgentLower.Contains("opera"))
        {
            browserInfo = "Opera";
        }

        // Try to determine department from IP address patterns
        if (!string.IsNullOrEmpty(ipAddress))
        {
            departmentInfo = DetermineDepartmentFromIp(ipAddress);
        }

        // Construct device identifier
        if (!string.IsNullOrEmpty(browserInfo))
        {
            if (!string.IsNullOrEmpty(departmentInfo))
            {
                return $"Desktop-{departmentInfo} ({browserInfo})";
            }
            return $"Desktop {browserInfo}";
        }

        // Fallback for Windows/Mac detection
        if (userAgentLower.Contains("windows"))
        {
            var windowsMatch = Regex.Match(userAgent, @"Windows NT (\d+\.\d+)", RegexOptions.IgnoreCase);
            if (windowsMatch.Success)
            {
                var version = windowsMatch.Groups[1].Value;
                var windowsVersion = version switch
                {
                    "10.0" => "10",
                    "6.3" => "8.1",
                    "6.2" => "8",
                    "6.1" => "7",
                    _ => version
                };
                
                if (!string.IsNullOrEmpty(departmentInfo))
                {
                    return $"Desktop-{departmentInfo} (Windows {windowsVersion})";
                }
                return $"Desktop Windows {windowsVersion}";
            }
            
            if (!string.IsNullOrEmpty(departmentInfo))
            {
                return $"Desktop-{departmentInfo} (Windows)";
            }
            return "Desktop Windows";
        }

        if (userAgentLower.Contains("macintosh") || userAgentLower.Contains("mac os"))
        {
            if (!string.IsNullOrEmpty(departmentInfo))
            {
                return $"Desktop-{departmentInfo} (Mac)";
            }
            return "Desktop Mac";
        }

        if (userAgentLower.Contains("linux"))
        {
            if (!string.IsNullOrEmpty(departmentInfo))
            {
                return $"Desktop-{departmentInfo} (Linux)";
            }
            return "Desktop Linux";
        }

        return null;
    }

    private string? ExtractApplicationIdentifier(string userAgent, string userAgentLower)
    {
        // Check for specific applications
        if (userAgentLower.Contains("postman"))
        {
            return "Postman API Client";
        }

        if (userAgentLower.Contains("insomnia"))
        {
            return "Insomnia API Client";
        }

        if (userAgentLower.Contains("curl"))
        {
            return "cURL Client";
        }

        if (userAgentLower.Contains("wget"))
        {
            return "Wget Client";
        }

        if (userAgentLower.Contains("python"))
        {
            return "Python Application";
        }

        if (userAgentLower.Contains("java"))
        {
            return "Java Application";
        }

        if (userAgentLower.Contains("node"))
        {
            return "Node.js Application";
        }

        // Check for mobile apps
        if (userAgentLower.Contains("thinkonerp"))
        {
            if (userAgentLower.Contains("android"))
            {
                return "ThinkOnERP Android App";
            }
            if (userAgentLower.Contains("ios"))
            {
                return "ThinkOnERP iOS App";
            }
            return "ThinkOnERP Mobile App";
        }

        return null;
    }

    private string DetermineDepartmentFromIp(string ipAddress)
    {
        // Define IP ranges for different departments
        var departmentRanges = new Dictionary<string, string[]>
        {
            { "HR", new[] { "192.168.10.", "10.0.10.", "172.16.10." } },
            { "ACC", new[] { "192.168.20.", "10.0.20.", "172.16.20." } },
            { "IT", new[] { "192.168.30.", "10.0.30.", "172.16.30." } },
            { "SALES", new[] { "192.168.40.", "10.0.40.", "172.16.40." } },
            { "MGMT", new[] { "192.168.50.", "10.0.50.", "172.16.50." } },
            { "SUP", new[] { "192.168.60.", "10.0.60.", "172.16.60." } }
        };

        foreach (var (department, ranges) in departmentRanges)
        {
            foreach (var range in ranges)
            {
                if (ipAddress.StartsWith(range))
                {
                    // Extract device number from last octet
                    var match = Regex.Match(ipAddress, @"\.(\d+)$");
                    if (match.Success)
                    {
                        var deviceNumber = match.Groups[1].Value;
                        return $"{department}-{deviceNumber.PadLeft(2, '0')}";
                    }
                    return department;
                }
            }
        }

        return string.Empty;
    }

    private string GenerateIpBasedDeviceIdentifier(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return "Unknown Device";
        }

        // Try to determine device type from IP pattern
        var departmentInfo = DetermineDepartmentFromIp(ipAddress);
        if (!string.IsNullOrEmpty(departmentInfo))
        {
            return $"Device-{departmentInfo}";
        }

        // Extract last octet for generic device identification
        var ipMatch = Regex.Match(ipAddress, @"\.(\d+)$");
        if (ipMatch.Success)
        {
            var lastOctet = ipMatch.Groups[1].Value;
            return $"Device-{lastOctet}";
        }

        return $"Unknown Device ({ipAddress})";
    }

    /// <inheritdoc/>
    public async Task<string> DetermineBusinessModuleAsync(string entityType, string? endpointPath)
    {
        try
        {
            // First try to match by entity type
            var entityTypeLower = entityType.ToLowerInvariant();
            if (ModuleMappings.TryGetValue(entityTypeLower, out var moduleFromEntity))
            {
                return moduleFromEntity;
            }

            // Then try to match by endpoint path
            if (!string.IsNullOrEmpty(endpointPath))
            {
                var endpointLower = endpointPath.ToLowerInvariant();
                foreach (var (pathPattern, module) in ModuleMappings)
                {
                    if (endpointLower.Contains(pathPattern))
                    {
                        return module;
                    }
                }
            }

            // Fallback based on common patterns
            return entityTypeLower switch
            {
                var e when e.Contains("user") || e.Contains("employee") => "HR",
                var e when e.Contains("company") || e.Contains("branch") || e.Contains("system") => "Administration",
                var e when e.Contains("role") || e.Contains("permission") || e.Contains("auth") => "Security",
                var e when e.Contains("currency") || e.Contains("fiscal") || e.Contains("accounting") => "Accounting",
                var e when e.Contains("ticket") || e.Contains("support") => "Support",
                var e when e.Contains("pos") || e.Contains("sale") => "POS",
                var e when e.Contains("inventory") || e.Contains("product") => "Inventory",
                var e when e.Contains("customer") || e.Contains("crm") => "CRM",
                _ => "System"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to determine business module for entity type: {EntityType}, endpoint: {EndpointPath}", 
                entityType, endpointPath);
            return await Task.FromResult("System");
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateErrorCodeAsync(string exceptionType, string entityType)
    {
        try
        {
            // Get prefix from exception type
            var prefix = "UNKNOWN";
            if (ErrorCodePrefixes.TryGetValue(exceptionType, out var foundPrefix))
            {
                prefix = foundPrefix;
            }
            else
            {
                // Try to extract a meaningful prefix from the exception type name
                var match = Regex.Match(exceptionType, @"^(\w+)Exception$");
                if (match.Success)
                {
                    prefix = match.Groups[1].Value.ToUpperInvariant();
                }
            }

            // Get module suffix from entity type
            var module = await DetermineBusinessModuleAsync(entityType, null);
            var moduleSuffix = module.ToUpperInvariant() switch
            {
                "HR" => "HR",
                "ADMINISTRATION" => "ADMIN",
                "SECURITY" => "SEC",
                "ACCOUNTING" => "ACC",
                "SUPPORT" => "SUP",
                "POS" => "POS",
                "INVENTORY" => "INV",
                "CRM" => "CRM",
                _ => "SYS"
            };

            // Generate a simple sequential number (in a real implementation, this might be stored/incremented)
            var sequenceNumber = (Math.Abs(exceptionType.GetHashCode() + entityType.GetHashCode()) % 999) + 1;

            return $"{prefix}_{moduleSuffix}_{sequenceNumber:D3}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate error code for exception type: {ExceptionType}, entity type: {EntityType}", 
                exceptionType, entityType);
            return await Task.FromResult("UNKNOWN_SYS_001");
        }
    }

    #region Private Helper Methods

    private string GenerateInsertDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        return $"New {entityName} created by {auditEntry.ActorName ?? "System"}";
    }

    private string GenerateUpdateDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "System";
        
        // Try to provide more specific information about what was updated
        if (!string.IsNullOrEmpty(auditEntry.NewValue) && auditEntry.NewValue.Length < 200)
        {
            try
            {
                // Parse JSON to see what fields were changed
                var newData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditEntry.NewValue);
                if (newData?.Count == 1)
                {
                    var changedField = newData.Keys.First();
                    return $"{entityName} {changedField} updated by {actorName}";
                }
                else if (newData?.Count > 1)
                {
                    return $"{entityName} updated ({newData.Count} fields changed) by {actorName}";
                }
            }
            catch
            {
                // Fall back to generic message if JSON parsing fails
            }
        }
        
        return $"{entityName} updated by {actorName}";
    }

    private string GenerateDeleteDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        return $"{entityName} deleted by {auditEntry.ActorName ?? "System"}";
    }

    private string GenerateLoginDescription(AuditLogEntry auditEntry)
    {
        var actorName = auditEntry.ActorName ?? "Unknown User";
        var companyName = auditEntry.CompanyName ?? "Unknown Company";
        var branchName = auditEntry.BranchName ?? "Unknown Branch";
        
        if (!string.IsNullOrEmpty(auditEntry.IpAddress))
        {
            return $"User {actorName} logged in to {companyName} - {branchName} from {auditEntry.IpAddress}";
        }
        
        return $"User {actorName} logged in to {companyName} - {branchName}";
    }

    private string GenerateLogoutDescription(AuditLogEntry auditEntry)
    {
        return $"User {auditEntry.ActorName ?? "Unknown"} logged out";
    }

    private async Task<string> GenerateExceptionDescription(AuditLogEntry auditEntry)
    {
        if (!string.IsNullOrEmpty(auditEntry.ExceptionMessage))
        {
            // Convert technical exception to business-friendly message
            var message = auditEntry.ExceptionMessage.ToLowerInvariant();
            var entityName = GetFriendlyEntityName(auditEntry.EntityType);
            var businessModule = await DetermineBusinessModuleAsync(auditEntry.EntityType, auditEntry.EndpointPath);
            
            // Database-related errors
            if (message.Contains("timeout") || message.Contains("ora-00942") || message.Contains("ora-12170"))
                return $"Database connection timeout in {businessModule} - please try again or contact support";
            if (message.Contains("connection") || message.Contains("ora-12541") || message.Contains("ora-12514"))
                return $"Database connection issue in {businessModule} - please contact IT support";
            if (message.Contains("ora-00001") || message.Contains("duplicate") || message.Contains("unique"))
                return $"Duplicate {entityName} entry - record already exists";
            if (message.Contains("ora-02292") || message.Contains("foreign key"))
                return $"Cannot delete {entityName} - it is being used by other records";
            if (message.Contains("ora-01400") || message.Contains("cannot be null"))
                return $"Required information missing for {entityName} - please fill all mandatory fields";
            
            // Authentication and authorization errors
            if (message.Contains("unauthorized") || message.Contains("access denied") || message.Contains("401"))
                return $"Access denied to {entityName} - insufficient permissions";
            if (message.Contains("forbidden") || message.Contains("403"))
                return $"Operation not allowed on {entityName} - contact your administrator";
            if (message.Contains("token") && message.Contains("expired"))
                return "Your session has expired - please log in again";
            if (message.Contains("invalid credentials") || message.Contains("authentication failed"))
                return "Invalid login credentials - please check username and password";
            
            // Validation errors
            if (message.Contains("validation") || message.Contains("invalid format"))
                return $"Data validation error for {entityName} - please check your input format";
            if (message.Contains("required field") || message.Contains("mandatory"))
                return $"Required fields missing for {entityName} - please complete all mandatory information";
            if (message.Contains("email") && message.Contains("invalid"))
                return "Invalid email address format - please enter a valid email";
            if (message.Contains("phone") && message.Contains("invalid"))
                return "Invalid phone number format - please enter a valid phone number";
            
            // Business logic errors
            if (message.Contains("not found") || message.Contains("404"))
                return $"Requested {entityName} not found - it may have been deleted or moved";
            if (message.Contains("already exists") || message.Contains("conflict"))
                return $"{entityName} already exists with the same information";
            if (message.Contains("insufficient funds") || message.Contains("balance"))
                return "Insufficient account balance for this transaction";
            if (message.Contains("expired") && !message.Contains("token"))
                return $"{entityName} has expired and cannot be used";
            
            // File and upload errors
            if (message.Contains("file size") || message.Contains("too large"))
                return "File size too large - please upload a smaller file";
            if (message.Contains("file type") || message.Contains("not supported"))
                return "File type not supported - please upload a valid file format";
            if (message.Contains("upload failed") || message.Contains("storage"))
                return "File upload failed - please try again or contact support";
            
            // Network and service errors
            if (message.Contains("service unavailable") || message.Contains("503"))
                return $"{businessModule} service temporarily unavailable - please try again later";
            if (message.Contains("bad gateway") || message.Contains("502"))
                return "Service communication error - please try again or contact support";
            if (message.Contains("request timeout") || message.Contains("408"))
                return "Request timeout - operation took too long, please try again";
            
            // Permission and role errors
            if (message.Contains("role") && message.Contains("required"))
                return $"Specific role required to access {entityName} - contact your administrator";
            if (message.Contains("company") && message.Contains("access"))
                return "Access restricted to your company data only";
            if (message.Contains("branch") && message.Contains("access"))
                return "Access restricted to your branch data only";
            
            // System and configuration errors
            if (message.Contains("configuration") || message.Contains("config"))
                return $"System configuration error in {businessModule} - contact IT support";
            if (message.Contains("license") || message.Contains("subscription"))
                return "License or subscription issue - contact your administrator";
            if (message.Contains("maintenance") || message.Contains("scheduled"))
                return $"{businessModule} is under maintenance - please try again later";
            
            // Exception type-specific handling
            if (!string.IsNullOrEmpty(auditEntry.ExceptionType))
            {
                var exceptionType = auditEntry.ExceptionType.ToLowerInvariant();
                if (exceptionType.Contains("sqlexception") || exceptionType.Contains("oracleexception"))
                    return $"Database error in {businessModule} - please contact support with error details";
                if (exceptionType.Contains("argumentexception") || exceptionType.Contains("argumentnullexception"))
                    return $"Invalid input provided for {entityName} - please check your data";
                if (exceptionType.Contains("invalidoperationexception"))
                    return $"Operation not valid for current {entityName} state";
                if (exceptionType.Contains("notimplementedexception"))
                    return $"Feature not yet available for {entityName} - coming soon";
            }
            
            // Truncate long technical messages and make them more user-friendly
            if (auditEntry.ExceptionMessage.Length > 100)
            {
                return $"System error in {businessModule} - please contact support (Error: {auditEntry.ExceptionMessage.Substring(0, 50)}...)";
            }
            
            // Default fallback with context
            return $"System error in {businessModule} - {auditEntry.ExceptionMessage}";
        }

        return $"System error occurred in {GetFriendlyEntityName(auditEntry.EntityType)}";
    }

    private string GenerateAuthorizationFailureDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "User";
        return $"Access denied to {entityName} for {actorName} - insufficient permissions";
    }

    private string GenerateStatusChangeDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "System";
        
        // Try to extract old and new status from the audit data
        if (!string.IsNullOrEmpty(auditEntry.OldValue) && !string.IsNullOrEmpty(auditEntry.NewValue))
        {
            try
            {
                var oldData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditEntry.OldValue);
                var newData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditEntry.NewValue);
                
                if (oldData?.ContainsKey("status") == true && newData?.ContainsKey("status") == true)
                {
                    return $"{entityName} status changed from {oldData["status"]} to {newData["status"]} by {actorName}";
                }
            }
            catch
            {
                // Fall back to generic message if JSON parsing fails
            }
        }
        
        return $"{entityName} status changed by {actorName}";
    }

    private string GenerateAssignmentChangeDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "System";
        
        // Try to extract assignment information from the audit data
        if (!string.IsNullOrEmpty(auditEntry.NewValue))
        {
            try
            {
                var newData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditEntry.NewValue);
                if (newData?.ContainsKey("assignedTo") == true)
                {
                    return $"{entityName} assigned to {newData["assignedTo"]} by {actorName}";
                }
                if (newData?.ContainsKey("assignedToUserId") == true)
                {
                    return $"{entityName} assignment changed by {actorName}";
                }
            }
            catch
            {
                // Fall back to generic message if JSON parsing fails
            }
        }
        
        return $"{entityName} assignment changed by {actorName}";
    }

    private string GenerateCommentDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "User";
        
        if (auditEntry.EntityType.ToLowerInvariant().Contains("ticket"))
        {
            return $"Comment added to {entityName} by {actorName}";
        }
        
        return $"Comment added to {entityName} by {actorName}";
    }

    private string GenerateAttachmentUploadDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "User";
        
        // Try to extract file information from the audit data
        if (!string.IsNullOrEmpty(auditEntry.NewValue))
        {
            try
            {
                var newData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditEntry.NewValue);
                if (newData?.ContainsKey("fileName") == true)
                {
                    return $"File '{newData["fileName"]}' uploaded to {entityName} by {actorName}";
                }
            }
            catch
            {
                // Fall back to generic message if JSON parsing fails
            }
        }
        
        return $"File uploaded to {entityName} by {actorName}";
    }

    private string GenerateAttachmentDownloadDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "User";
        
        // Try to extract file information from the audit data
        if (!string.IsNullOrEmpty(auditEntry.OldValue))
        {
            try
            {
                var oldData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditEntry.OldValue);
                if (oldData?.ContainsKey("fileName") == true)
                {
                    return $"File '{oldData["fileName"]}' downloaded from {entityName} by {actorName}";
                }
            }
            catch
            {
                // Fall back to generic message if JSON parsing fails
            }
        }
        
        return $"File downloaded from {entityName} by {actorName}";
    }

    private string GenerateSearchDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        var actorName = auditEntry.ActorName ?? "User";
        
        // Try to extract search terms from the audit data
        if (!string.IsNullOrEmpty(auditEntry.NewValue))
        {
            try
            {
                var searchData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(auditEntry.NewValue);
                if (searchData?.ContainsKey("searchTerm") == true)
                {
                    var searchTerm = searchData["searchTerm"].ToString();
                    if (!string.IsNullOrEmpty(searchTerm) && searchTerm.Length <= 50)
                    {
                        return $"Search performed for '{searchTerm}' in {entityName} by {actorName}";
                    }
                }
            }
            catch
            {
                // Fall back to generic message if JSON parsing fails
            }
        }
        
        return $"Search performed in {entityName} by {actorName}";
    }

    private string GenerateViewDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        return $"{entityName} viewed by {auditEntry.ActorName ?? "User"}";
    }

    private string GenerateGenericDescription(AuditLogEntry auditEntry)
    {
        var entityName = GetFriendlyEntityName(auditEntry.EntityType);
        return $"{auditEntry.Action} performed on {entityName} by {auditEntry.ActorName ?? "System"}";
    }

    private string GetFriendlyEntityName(string entityType)
    {
        return entityType.ToLowerInvariant() switch
        {
            // Core entities
            "ticket" => "Support Ticket",
            "user" => "User Account",
            "company" => "Company",
            "branch" => "Branch",
            "role" => "User Role",
            "permission" => "Permission",
            "currency" => "Currency",
            "fiscalyear" => "Fiscal Year",
            
            // Ticket-related entities
            "ticketcomment" => "Ticket Comment",
            "ticketattachment" => "Ticket Attachment",
            "ticketstatus" => "Ticket Status",
            "ticketpriority" => "Ticket Priority",
            "ticketcategory" => "Ticket Category",
            
            // System entities
            "system" => "System",
            "auditlog" => "Audit Log",
            "configuration" => "System Configuration",
            "setting" => "System Setting",
            
            // HR module entities
            "employee" => "Employee",
            "department" => "Department",
            "position" => "Job Position",
            "payroll" => "Payroll",
            "attendance" => "Attendance Record",
            "leave" => "Leave Request",
            "performance" => "Performance Review",
            
            // Accounting module entities
            "account" => "Account",
            "transaction" => "Transaction",
            "invoice" => "Invoice",
            "payment" => "Payment",
            "receipt" => "Receipt",
            "journal" => "Journal Entry",
            "ledger" => "General Ledger",
            "budget" => "Budget",
            "expense" => "Expense",
            "revenue" => "Revenue",
            
            // POS module entities
            "sale" => "Sale Transaction",
            "product" => "Product",
            "inventory" => "Inventory Item",
            "customer" => "Customer",
            "supplier" => "Supplier",
            "order" => "Order",
            "quotation" => "Quotation",
            "delivery" => "Delivery",
            "return" => "Return",
            "discount" => "Discount",
            "promotion" => "Promotion",
            
            // Security entities
            "session" => "User Session",
            "token" => "Access Token",
            "login" => "Login Attempt",
            "logout" => "Logout Event",
            
            // File and document entities
            "document" => "Document",
            "attachment" => "File Attachment",
            "report" => "Report",
            "export" => "Data Export",
            "import" => "Data Import",
            
            // Default fallback
            _ => entityType.Replace("_", " ").Replace("-", " ")
        };
    }

    #endregion
}
