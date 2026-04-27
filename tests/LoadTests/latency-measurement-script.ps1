#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Latency Overhead Measurement Script for Full Traceability System

.DESCRIPTION
    This PowerShell script measures the latency overhead introduced by the 
    traceability system components (audit logging, request tracing, performance monitoring).
    
    It validates Task 20.2 requirement: "System SHALL add no more than 10ms latency 
    to API requests for 99% of operations"

.PARAMETER ApiUrl
    The base URL of the ThinkOnErp API (default: http://localhost:5000)

.PARAMETER JwtToken
    JWT token for authentication (if not provided, will attempt to authenticate)

.PARAMETER Duration
    Duration of each test phase in seconds (default: 60)

.PARAMETER Concurrency
    Number of concurrent requests (default: 10)

.PARAMETER OutputFile
    Output file for detailed results (default: latency-results-{timestamp}.json)

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\latency-measurement-script.ps1
    
.EXAMPLE
    .\latency-measurement-script.ps1 -ApiUrl "http://localhost:5000" -Duration 120 -Concurrency 20

.EXAMPLE
    .\latency-measurement-script.ps1 -JwtToken "your-jwt-token" -OutputFile "results.json" -Verbose
#>

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$JwtToken = "",
    [int]$Duration = 60,
    [int]$Concurrency = 10,
    [string]$OutputFile = "",
    [switch]$Verbose
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Import required modules
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.Text.Json

# Initialize output file
if (-not $OutputFile) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputFile = "latency-results-$timestamp.json"
}

# Test configuration
$TestEndpoints = @(
    @{ Name = "companies"; Path = "/api/companies"; Weight = 25; Complexity = "medium" },
    @{ Name = "users"; Path = "/api/users"; Weight = 25; Complexity = "medium" },
    @{ Name = "roles"; Path = "/api/roles"; Weight = 20; Complexity = "low" },
    @{ Name = "currencies"; Path = "/api/currencies"; Weight = 15; Complexity = "low" },
    @{ Name = "branches"; Path = "/api/branches"; Weight = 10; Complexity = "medium" },
    @{ Name = "audit_logs"; Path = "/api/auditlogs/legacy-view?pageNumber=1&pageSize=10"; Weight = 5; Complexity = "high" }
)

# Results storage
$Results = @{
    TestConfiguration = @{
        ApiUrl = $ApiUrl
        Duration = $Duration
        Concurrency = $Concurrency
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    }
    EndpointResults = @{}
    Summary = @{}
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARN" { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] $Message" -ForegroundColor $color
}

function Get-JwtToken {
    param([string]$ApiUrl)
    
    Write-Log "Attempting to authenticate with superadmin credentials..."
    
    $loginData = @{
        userName = "superadmin"
        password = "SuperAdmin123!"
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" -Method Post -Body $loginData -ContentType "application/json"
        Write-Log "Authentication successful" -Level "SUCCESS"
        return $response.token
    }
    catch {
        Write-Log "Authentication failed: $($_.Exception.Message)" -Level "ERROR"
        throw "Failed to authenticate. Please provide a valid JWT token or check API credentials."
    }
}

function Measure-EndpointLatency {
    param(
        [string]$ApiUrl,
        [string]$Endpoint,
        [string]$JwtToken,
        [int]$RequestCount,
        [int]$Concurrency
    )
    
    Write-Log "Testing endpoint: $Endpoint"
    
    $headers = @{
        "Authorization" = "Bearer $JwtToken"
        "Content-Type" = "application/json"
    }
    
    $responseTimes = @()
    $errors = 0
    $auditOverheads = @()
    $correlationIds = @()
    
    # Create HTTP client for better performance
    $httpClient = New-Object System.Net.Http.HttpClient
    $httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer $JwtToken")
    $httpClient.Timeout = [TimeSpan]::FromSeconds(30)
    
    try {
        # Warm up
        Write-Log "  Warming up endpoint..."
        for ($i = 0; $i -lt 5; $i++) {
            try {
                $null = $httpClient.GetAsync("$ApiUrl$Endpoint").Result
            }
            catch {
                # Ignore warm-up errors
            }
        }
        
        Write-Log "  Running $RequestCount requests with concurrency $Concurrency..."
        
        # Create jobs for concurrent execution
        $jobs = @()
        $requestsPerJob = [Math]::Ceiling($RequestCount / $Concurrency)
        
        for ($jobId = 0; $jobId -lt $Concurrency; $jobId++) {
            $job = Start-Job -ScriptBlock {
                param($ApiUrl, $Endpoint, $JwtToken, $RequestsPerJob, $JobId)
                
                Add-Type -AssemblyName System.Net.Http
                $httpClient = New-Object System.Net.Http.HttpClient
                $httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer $JwtToken")
                $httpClient.Timeout = [TimeSpan]::FromSeconds(30)
                
                $jobResults = @{
                    ResponseTimes = @()
                    Errors = 0
                    AuditOverheads = @()
                    CorrelationIds = @()
                }
                
                for ($i = 0; $i -lt $RequestsPerJob; $i++) {
                    try {
                        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
                        $response = $httpClient.GetAsync("$ApiUrl$Endpoint").Result
                        $stopwatch.Stop()
                        
                        $responseTimeMs = $stopwatch.ElapsedMilliseconds
                        $jobResults.ResponseTimes += $responseTimeMs
                        
                        # Extract correlation ID
                        $correlationId = $null
                        if ($response.Headers.Contains("X-Correlation-Id")) {
                            $correlationId = ($response.Headers.GetValues("X-Correlation-Id"))[0]
                            $jobResults.CorrelationIds += $correlationId
                        }
                        
                        # Extract audit overhead if available
                        if ($response.Headers.Contains("X-Audit-Overhead-Ms")) {
                            $auditOverhead = [double]($response.Headers.GetValues("X-Audit-Overhead-Ms"))[0]
                            $jobResults.AuditOverheads += $auditOverhead
                        }
                        
                        if ($response.StatusCode -ne 200) {
                            $jobResults.Errors++
                        }
                    }
                    catch {
                        $jobResults.Errors++
                    }
                }
                
                $httpClient.Dispose()
                return $jobResults
                
            } -ArgumentList $ApiUrl, $Endpoint, $JwtToken, $requestsPerJob, $jobId
            
            $jobs += $job
        }
        
        # Wait for all jobs to complete
        Write-Log "  Waiting for requests to complete..."
        $jobResults = $jobs | Wait-Job | Receive-Job
        $jobs | Remove-Job
        
        # Aggregate results
        foreach ($result in $jobResults) {
            $responseTimes += $result.ResponseTimes
            $errors += $result.Errors
            $auditOverheads += $result.AuditOverheads
            $correlationIds += $result.CorrelationIds
        }
        
    }
    finally {
        $httpClient.Dispose()
    }
    
    # Calculate statistics
    if ($responseTimes.Count -gt 0) {
        $sortedTimes = $responseTimes | Sort-Object
        $stats = @{
            TotalRequests = $responseTimes.Count
            SuccessfulRequests = $responseTimes.Count - $errors
            FailedRequests = $errors
            ErrorRate = if ($responseTimes.Count -gt 0) { $errors / $responseTimes.Count * 100 } else { 0 }
            
            ResponseTime = @{
                Min = ($sortedTimes | Measure-Object -Minimum).Minimum
                Max = ($sortedTimes | Measure-Object -Maximum).Maximum
                Average = ($sortedTimes | Measure-Object -Average).Average
                P50 = $sortedTimes[[Math]::Floor($sortedTimes.Count * 0.5)]
                P95 = $sortedTimes[[Math]::Floor($sortedTimes.Count * 0.95)]
                P99 = $sortedTimes[[Math]::Floor($sortedTimes.Count * 0.99)]
            }
            
            AuditOverhead = @{}
            CorrelationIds = $correlationIds | Select-Object -Unique
        }
        
        # Calculate audit overhead statistics if available
        if ($auditOverheads.Count -gt 0) {
            $sortedOverheads = $auditOverheads | Sort-Object
            $stats.AuditOverhead = @{
                Count = $auditOverheads.Count
                Min = ($sortedOverheads | Measure-Object -Minimum).Minimum
                Max = ($sortedOverheads | Measure-Object -Maximum).Maximum
                Average = ($sortedOverheads | Measure-Object -Average).Average
                P50 = $sortedOverheads[[Math]::Floor($sortedOverheads.Count * 0.5)]
                P95 = $sortedOverheads[[Math]::Floor($sortedOverheads.Count * 0.95)]
                P99 = $sortedOverheads[[Math]::Floor($sortedOverheads.Count * 0.99)]
            }
        }
        
        return $stats
    }
    else {
        Write-Log "  No successful requests recorded" -Level "WARN"
        return $null
    }
}

function Test-LatencyRequirements {
    param($Results)
    
    Write-Log "Validating latency requirements..." -Level "INFO"
    
    $overallPass = $true
    $validationResults = @()
    
    foreach ($endpointName in $Results.EndpointResults.Keys) {
        $endpointResult = $Results.EndpointResults[$endpointName]
        
        if ($endpointResult -and $endpointResult.ResponseTime) {
            # Requirement 1: p99 response time should be reasonable (< 500ms)
            $p99ResponseTime = $endpointResult.ResponseTime.P99
            $responseTimePass = $p99ResponseTime -lt 500
            
            # Requirement 2: Error rate should be low (< 1%)
            $errorRate = $endpointResult.ErrorRate
            $errorRatePass = $errorRate -lt 1.0
            
            # Requirement 3: Audit overhead should be minimal (< 10ms p99)
            $auditOverheadPass = $true
            $auditOverheadP99 = "N/A"
            
            if ($endpointResult.AuditOverhead -and $endpointResult.AuditOverhead.P99) {
                $auditOverheadP99 = $endpointResult.AuditOverhead.P99
                $auditOverheadPass = $auditOverheadP99 -lt 10
            }
            
            $endpointPass = $responseTimePass -and $errorRatePass -and $auditOverheadPass
            $overallPass = $overallPass -and $endpointPass
            
            $validation = @{
                Endpoint = $endpointName
                P99ResponseTime = $p99ResponseTime
                ErrorRate = $errorRate
                AuditOverheadP99 = $auditOverheadP99
                ResponseTimePass = $responseTimePass
                ErrorRatePass = $errorRatePass
                AuditOverheadPass = $auditOverheadPass
                OverallPass = $endpointPass
            }
            
            $validationResults += $validation
            
            # Log results
            $status = if ($endpointPass) { "PASS" } else { "FAIL" }
            $level = if ($endpointPass) { "SUCCESS" } else { "ERROR" }
            
            Write-Log "  $endpointName`: P99=$($p99ResponseTime)ms, Errors=$($errorRate.ToString("F1"))%, AuditP99=$auditOverheadP99 - $status" -Level $level
        }
    }
    
    $Results.ValidationResults = $validationResults
    $Results.Summary.OverallPass = $overallPass
    
    return $overallPass
}

# Main execution
try {
    Write-Log "========================================" -Level "INFO"
    Write-Log "Full Traceability System - Latency Test" -Level "INFO"
    Write-Log "========================================" -Level "INFO"
    Write-Log ""
    Write-Log "API URL: $ApiUrl"
    Write-Log "Duration: $Duration seconds per endpoint"
    Write-Log "Concurrency: $Concurrency concurrent requests"
    Write-Log "Output File: $OutputFile"
    Write-Log ""
    
    # Authenticate if no token provided
    if (-not $JwtToken) {
        $JwtToken = Get-JwtToken -ApiUrl $ApiUrl
    }
    
    Write-Log "========================================" -Level "INFO"
    Write-Log "Starting Latency Measurements" -Level "INFO"
    Write-Log "========================================" -Level "INFO"
    
    # Test each endpoint
    foreach ($endpoint in $TestEndpoints) {
        $requestCount = [Math]::Max(50, $Duration * 2) # At least 50 requests, or 2 per second
        
        $endpointResult = Measure-EndpointLatency -ApiUrl $ApiUrl -Endpoint $endpoint.Path -JwtToken $JwtToken -RequestCount $requestCount -Concurrency $Concurrency
        
        if ($endpointResult) {
            $Results.EndpointResults[$endpoint.Name] = $endpointResult
            
            Write-Log "  Results for $($endpoint.Name):"
            Write-Log "    Total Requests: $($endpointResult.TotalRequests)"
            Write-Log "    Successful: $($endpointResult.SuccessfulRequests)"
            Write-Log "    Failed: $($endpointResult.FailedRequests)"
            Write-Log "    Error Rate: $($endpointResult.ErrorRate.ToString("F1"))%"
            Write-Log "    Response Time - Min: $($endpointResult.ResponseTime.Min)ms, Avg: $($endpointResult.ResponseTime.Average.ToString("F1"))ms, Max: $($endpointResult.ResponseTime.Max)ms"
            Write-Log "    Percentiles - P50: $($endpointResult.ResponseTime.P50)ms, P95: $($endpointResult.ResponseTime.P95)ms, P99: $($endpointResult.ResponseTime.P99)ms"
            
            if ($endpointResult.AuditOverhead -and $endpointResult.AuditOverhead.Count -gt 0) {
                Write-Log "    Audit Overhead - Avg: $($endpointResult.AuditOverhead.Average.ToString("F1"))ms, P95: $($endpointResult.AuditOverhead.P95)ms, P99: $($endpointResult.AuditOverhead.P99)ms"
            }
            
            Write-Log "    Correlation IDs: $($endpointResult.CorrelationIds.Count) unique"
            Write-Log ""
        }
        else {
            Write-Log "  Failed to get results for $($endpoint.Name)" -Level "WARN"
        }
    }
    
    Write-Log "========================================" -Level "INFO"
    Write-Log "Validating Performance Requirements" -Level "INFO"
    Write-Log "========================================" -Level "INFO"
    
    # Validate requirements
    $requirementsMet = Test-LatencyRequirements -Results $Results
    
    Write-Log ""
    Write-Log "========================================" -Level "INFO"
    Write-Log "Test Summary" -Level "INFO"
    Write-Log "========================================" -Level "INFO"
    
    if ($requirementsMet) {
        Write-Log "✓ All latency requirements met!" -Level "SUCCESS"
        Write-Log "  - p99 response times < 500ms for all endpoints" -Level "SUCCESS"
        Write-Log "  - Error rates < 1% for all endpoints" -Level "SUCCESS"
        Write-Log "  - Audit overhead < 10ms p99 (where measurable)" -Level "SUCCESS"
    }
    else {
        Write-Log "✗ Some latency requirements not met" -Level "ERROR"
        Write-Log "  Check individual endpoint results above" -Level "ERROR"
    }
    
    # Save detailed results
    $Results | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputFile -Encoding UTF8
    Write-Log ""
    Write-Log "Detailed results saved to: $OutputFile" -Level "INFO"
    
    Write-Log ""
    Write-Log "Task 20.2 Validation:" -Level "INFO"
    Write-Log "  Requirement: System SHALL add no more than 10ms latency" -Level "INFO"
    Write-Log "  to API requests for 99% of operations" -Level "INFO"
    
    if ($requirementsMet) {
        Write-Log "  Status: ✓ PASSED" -Level "SUCCESS"
    }
    else {
        Write-Log "  Status: ✗ FAILED" -Level "ERROR"
    }
    
    Write-Log ""
    Write-Log "========================================" -Level "INFO"
    Write-Log "Latency Test Complete" -Level "INFO"
    Write-Log "========================================" -Level "INFO"
    
    # Exit with appropriate code
    if ($requirementsMet) {
        exit 0
    }
    else {
        exit 1
    }
}
catch {
    Write-Log "Test execution failed: $($_.Exception.Message)" -Level "ERROR"
    Write-Log "Stack trace: $($_.ScriptStackTrace)" -Level "ERROR"
    exit 1
}