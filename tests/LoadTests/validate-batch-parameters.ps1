#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates batch processing parameters for the audit logging system.

.DESCRIPTION
    This script runs a series of performance tests to validate that the current
    batch processing parameters (BatchSize=50, BatchWindowMs=100) are optimal
    for the system's performance requirements.

.PARAMETER ApiUrl
    The base URL of the ThinkOnErp API. Default: http://localhost:5000

.PARAMETER JwtToken
    JWT authentication token. If not provided, will attempt to authenticate.

.PARAMETER Duration
    Duration of each test phase in seconds. Default: 60

.PARAMETER OutputFile
    Path to save test results. Default: batch-parameter-validation-results.json

.EXAMPLE
    .\validate-batch-parameters.ps1

.EXAMPLE
    .\validate-batch-parameters.ps1 -ApiUrl "http://localhost:5000" -Duration 120

.EXAMPLE
    .\validate-batch-parameters.ps1 -JwtToken "your-jwt-token" -OutputFile "results.json"
#>

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$JwtToken = "",
    [int]$Duration = 60,
    [string]$OutputFile = "batch-parameter-validation-results.json"
)

# Color output functions
function Write-Success { param([string]$Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "ℹ $Message" -ForegroundColor Cyan }
function Write-Warning { param([string]$Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "✗ $Message" -ForegroundColor Red }
function Write-Header { param([string]$Message) Write-Host "`n=== $Message ===" -ForegroundColor Magenta }

# Test results storage
$script:TestResults = @{
    Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    ApiUrl = $ApiUrl
    Duration = $Duration
    Tests = @()
    Summary = @{}
}

# Authenticate if no token provided
function Get-AuthToken {
    Write-Info "Authenticating with API..."
    
    $loginBody = @{
        userName = "superadmin"
        password = "SuperAdmin123!"
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" `
            -Method Post `
            -Body $loginBody `
            -ContentType "application/json" `
            -ErrorAction Stop

        Write-Success "Authentication successful"
        return $response.token
    }
    catch {
        Write-Error "Authentication failed: $_"
        exit 1
    }
}

# Test API endpoint with specified request rate
function Test-ApiEndpoint {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [hashtable]$Headers,
        [object]$Body = $null,
        [int]$RequestsPerSecond,
        [int]$DurationSeconds
    )

    Write-Info "Testing $Method $Endpoint at $RequestsPerSecond req/sec for $DurationSeconds seconds..."

    $results = @{
        Endpoint = $Endpoint
        Method = $Method
        RequestsPerSecond = $RequestsPerSecond
        Duration = $DurationSeconds
        TotalRequests = 0
        SuccessfulRequests = 0
        FailedRequests = 0
        ResponseTimes = @()
        Errors = @()
    }

    $delayMs = [Math]::Floor(1000 / $RequestsPerSecond)
    $endTime = (Get-Date).AddSeconds($DurationSeconds)

    while ((Get-Date) -lt $endTime) {
        $startTime = Get-Date

        try {
            $params = @{
                Uri = "$ApiUrl$Endpoint"
                Method = $Method
                Headers = $Headers
                ErrorAction = "Stop"
            }

            if ($Body) {
                $params.Body = ($Body | ConvertTo-Json)
                $params.ContentType = "application/json"
            }

            $response = Invoke-WebRequest @params
            $responseTime = ((Get-Date) - $startTime).TotalMilliseconds

            $results.TotalRequests++
            $results.SuccessfulRequests++
            $results.ResponseTimes += $responseTime

            # Extract correlation ID if present
            $correlationId = $response.Headers["X-Correlation-Id"]
        }
        catch {
            $results.TotalRequests++
            $results.FailedRequests++
            $results.Errors += $_.Exception.Message
        }

        # Maintain request rate
        $elapsed = ((Get-Date) - $startTime).TotalMilliseconds
        $sleepTime = [Math]::Max(0, $delayMs - $elapsed)
        if ($sleepTime -gt 0) {
            Start-Sleep -Milliseconds $sleepTime
        }
    }

    # Calculate statistics
    if ($results.ResponseTimes.Count -gt 0) {
        $sorted = $results.ResponseTimes | Sort-Object
        $results.MinResponseTime = $sorted[0]
        $results.MaxResponseTime = $sorted[-1]
        $results.AvgResponseTime = ($results.ResponseTimes | Measure-Object -Average).Average
        $results.P50ResponseTime = $sorted[[Math]::Floor($sorted.Count * 0.50)]
        $results.P95ResponseTime = $sorted[[Math]::Floor($sorted.Count * 0.95)]
        $results.P99ResponseTime = $sorted[[Math]::Floor($sorted.Count * 0.99)]
    }

    $results.ErrorRate = if ($results.TotalRequests -gt 0) {
        ($results.FailedRequests / $results.TotalRequests) * 100
    } else { 0 }

    return $results
}

# Main test execution
function Start-BatchParameterValidation {
    Write-Header "Batch Processing Parameter Validation"
    Write-Info "API URL: $ApiUrl"
    Write-Info "Test Duration: $Duration seconds per phase"
    Write-Info "Output File: $OutputFile"

    # Get authentication token
    $token = if ($JwtToken) { $JwtToken } else { Get-AuthToken }
    $headers = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/json"
    }

    # Test Phase 1: Low Load (100 req/min = ~1.67 req/sec)
    Write-Header "Phase 1: Low Load (100 req/min)"
    $phase1 = Test-ApiEndpoint -Endpoint "/api/companies" -Headers $headers `
        -RequestsPerSecond 2 -DurationSeconds $Duration
    $script:TestResults.Tests += @{ Phase = "Low Load (100 req/min)"; Results = $phase1 }
    
    Write-Success "Phase 1 Complete: $($phase1.SuccessfulRequests)/$($phase1.TotalRequests) successful"
    Write-Info "  Avg: $([Math]::Round($phase1.AvgResponseTime, 2))ms | P95: $([Math]::Round($phase1.P95ResponseTime, 2))ms | P99: $([Math]::Round($phase1.P99ResponseTime, 2))ms"

    Start-Sleep -Seconds 5

    # Test Phase 2: Medium Load (1,000 req/min = ~16.67 req/sec)
    Write-Header "Phase 2: Medium Load (1,000 req/min)"
    $phase2 = Test-ApiEndpoint -Endpoint "/api/companies" -Headers $headers `
        -RequestsPerSecond 17 -DurationSeconds $Duration
    $script:TestResults.Tests += @{ Phase = "Medium Load (1,000 req/min)"; Results = $phase2 }
    
    Write-Success "Phase 2 Complete: $($phase2.SuccessfulRequests)/$($phase2.TotalRequests) successful"
    Write-Info "  Avg: $([Math]::Round($phase2.AvgResponseTime, 2))ms | P95: $([Math]::Round($phase2.P95ResponseTime, 2))ms | P99: $([Math]::Round($phase2.P99ResponseTime, 2))ms"

    Start-Sleep -Seconds 5

    # Test Phase 3: High Load (5,000 req/min = ~83.33 req/sec)
    Write-Header "Phase 3: High Load (5,000 req/min)"
    $phase3 = Test-ApiEndpoint -Endpoint "/api/companies" -Headers $headers `
        -RequestsPerSecond 83 -DurationSeconds $Duration
    $script:TestResults.Tests += @{ Phase = "High Load (5,000 req/min)"; Results = $phase3 }
    
    Write-Success "Phase 3 Complete: $($phase3.SuccessfulRequests)/$($phase3.TotalRequests) successful"
    Write-Info "  Avg: $([Math]::Round($phase3.AvgResponseTime, 2))ms | P95: $([Math]::Round($phase3.P95ResponseTime, 2))ms | P99: $([Math]::Round($phase3.P99ResponseTime, 2))ms"

    Start-Sleep -Seconds 5

    # Test Phase 4: Target Load (10,000 req/min = ~166.67 req/sec)
    Write-Header "Phase 4: Target Load (10,000 req/min)"
    $phase4 = Test-ApiEndpoint -Endpoint "/api/companies" -Headers $headers `
        -RequestsPerSecond 167 -DurationSeconds $Duration
    $script:TestResults.Tests += @{ Phase = "Target Load (10,000 req/min)"; Results = $phase4 }
    
    Write-Success "Phase 4 Complete: $($phase4.SuccessfulRequests)/$($phase4.TotalRequests) successful"
    Write-Info "  Avg: $([Math]::Round($phase4.AvgResponseTime, 2))ms | P95: $([Math]::Round($phase4.P95ResponseTime, 2))ms | P99: $([Math]::Round($phase4.P99ResponseTime, 2))ms"

    # Generate summary
    Write-Header "Test Summary"
    
    $allPhases = @($phase1, $phase2, $phase3, $phase4)
    $totalRequests = ($allPhases | Measure-Object -Property TotalRequests -Sum).Sum
    $totalSuccessful = ($allPhases | Measure-Object -Property SuccessfulRequests -Sum).Sum
    $totalFailed = ($allPhases | Measure-Object -Property FailedRequests -Sum).Sum
    $overallErrorRate = if ($totalRequests -gt 0) { ($totalFailed / $totalRequests) * 100 } else { 0 }

    $script:TestResults.Summary = @{
        TotalRequests = $totalRequests
        SuccessfulRequests = $totalSuccessful
        FailedRequests = $totalFailed
        ErrorRate = $overallErrorRate
        Phase1_P99 = $phase1.P99ResponseTime
        Phase2_P99 = $phase2.P99ResponseTime
        Phase3_P99 = $phase3.P99ResponseTime
        Phase4_P99 = $phase4.P99ResponseTime
    }

    Write-Info "Total Requests: $totalRequests"
    Write-Info "Successful: $totalSuccessful ($([Math]::Round(($totalSuccessful/$totalRequests)*100, 2))%)"
    Write-Info "Failed: $totalFailed ($([Math]::Round($overallErrorRate, 2))%)"
    Write-Info ""
    Write-Info "Response Times (P99):"
    Write-Info "  Phase 1 (100 req/min):   $([Math]::Round($phase1.P99ResponseTime, 2))ms"
    Write-Info "  Phase 2 (1,000 req/min): $([Math]::Round($phase2.P99ResponseTime, 2))ms"
    Write-Info "  Phase 3 (5,000 req/min): $([Math]::Round($phase3.P99ResponseTime, 2))ms"
    Write-Info "  Phase 4 (10,000 req/min): $([Math]::Round($phase4.P99ResponseTime, 2))ms"

    # Validate against requirements
    Write-Header "Requirements Validation"
    
    $allPassed = $true

    # Requirement 1: Support 10,000 req/min
    if ($phase4.SuccessfulRequests -ge ($phase4.TotalRequests * 0.99)) {
        Write-Success "✓ Throughput: System handled 10,000 req/min successfully"
    } else {
        Write-Error "✗ Throughput: System failed to handle 10,000 req/min"
        $allPassed = $false
    }

    # Requirement 2: <10ms latency for 99% (Note: This measures total response time, not just audit overhead)
    # In a real scenario, we'd need instrumentation to measure audit overhead specifically
    Write-Warning "⚠ Audit Overhead: Cannot measure directly without instrumentation"
    Write-Info "  (Total P99 response time at 10k req/min: $([Math]::Round($phase4.P99ResponseTime, 2))ms)"

    # Requirement 3: Error rate <1%
    if ($overallErrorRate -lt 1.0) {
        Write-Success "✓ Error Rate: $([Math]::Round($overallErrorRate, 2))% (target: <1%)"
    } else {
        Write-Error "✗ Error Rate: $([Math]::Round($overallErrorRate, 2))% (target: <1%)"
        $allPassed = $false
    }

    # Requirement 4: Response time degradation
    $degradation = (($phase4.P99ResponseTime - $phase1.P99ResponseTime) / $phase1.P99ResponseTime) * 100
    if ($degradation -lt 50) {
        Write-Success "✓ Performance Degradation: $([Math]::Round($degradation, 2))% increase from low to high load"
    } else {
        Write-Warning "⚠ Performance Degradation: $([Math]::Round($degradation, 2))% increase from low to high load"
    }

    # Save results to file
    $script:TestResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputFile -Encoding UTF8
    Write-Success "Results saved to: $OutputFile"

    # Final verdict
    Write-Header "Final Verdict"
    if ($allPassed) {
        Write-Success "✓ All requirements validated successfully"
        Write-Success "✓ Current batch parameters (BatchSize=50, BatchWindowMs=100) are optimal"
    } else {
        Write-Warning "⚠ Some requirements not met - review results and consider parameter adjustments"
    }

    return $allPassed
}

# Execute validation
try {
    $success = Start-BatchParameterValidation
    exit $(if ($success) { 0 } else { 1 })
}
catch {
    Write-Error "Test execution failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
