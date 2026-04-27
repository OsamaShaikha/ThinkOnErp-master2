###############################################################################
# Simple Load Test Script for Full Traceability System (PowerShell)
# 
# This script uses Invoke-WebRequest for basic load testing on Windows.
# For comprehensive testing, use k6 (see README.md)
#
# Requirements:
# - PowerShell 5.1 or later
# - ThinkOnErp API running
# - Valid JWT token (optional, will authenticate automatically)
#
# Usage:
#   .\load-test-simple.ps1
#   .\load-test-simple.ps1 -ApiUrl "http://localhost:5000" -JwtToken "your-token"
###############################################################################

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$JwtToken = "",
    [int]$TargetRpm = 10000,
    [int]$Concurrency = 50,
    [int]$DurationSeconds = 60
)

# Calculate requests per second
$TargetRps = [math]::Floor($TargetRpm / 60)

Write-Host "========================================" -ForegroundColor Blue
Write-Host "Full Traceability System - Load Test" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue
Write-Host ""
Write-Host "API URL: " -NoNewline
Write-Host $ApiUrl -ForegroundColor Green
Write-Host "Target: " -NoNewline
Write-Host "$TargetRpm requests/minute ($TargetRps req/sec)" -ForegroundColor Green
Write-Host "Concurrency: " -NoNewline
Write-Host $Concurrency -ForegroundColor Green
Write-Host "Duration: " -NoNewline
Write-Host "$DurationSeconds seconds" -ForegroundColor Green
Write-Host ""

# Create results directory
$ResultsDir = ".\results"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
if (-not (Test-Path $ResultsDir)) {
    New-Item -ItemType Directory -Path $ResultsDir | Out-Null
}

# Authenticate if no token provided
if ([string]::IsNullOrEmpty($JwtToken)) {
    Write-Host "No JWT token provided, attempting to authenticate..." -ForegroundColor Yellow
    
    try {
        $loginBody = @{
            userName = "superadmin"
            password = "SuperAdmin123!"
        } | ConvertTo-Json
        
        $loginResponse = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" `
            -Method Post `
            -Body $loginBody `
            -ContentType "application/json"
        
        $JwtToken = $loginResponse.token
        Write-Host "Authentication successful" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "Authentication failed: $_" -ForegroundColor Red
        exit 1
    }
}

# Function to run a load test on an endpoint
function Run-LoadTest {
    param(
        [string]$Endpoint,
        [string]$Method,
        [string]$Description,
        [hashtable]$Body = $null
    )
    
    Write-Host "Testing: $Description" -ForegroundColor Blue
    Write-Host "Endpoint: $Endpoint"
    
    $headers = @{
        "Authorization" = "Bearer $JwtToken"
        "Content-Type" = "application/json"
    }
    
    $results = @{
        TotalRequests = 0
        SuccessfulRequests = 0
        FailedRequests = 0
        ResponseTimes = @()
        StartTime = Get-Date
    }
    
    $targetRequests = $TargetRps * $DurationSeconds
    $delayMs = [math]::Floor(1000 / $TargetRps)
    
    Write-Host "Running $targetRequests requests..." -ForegroundColor Yellow
    
    # Run requests
    $jobs = @()
    $requestCount = 0
    $startTime = Get-Date
    
    while ((Get-Date) -lt $startTime.AddSeconds($DurationSeconds)) {
        # Launch concurrent requests
        for ($i = 0; $i -lt $Concurrency -and $requestCount -lt $targetRequests; $i++) {
            $requestCount++
            
            $job = Start-Job -ScriptBlock {
                param($url, $headers, $method, $body)
                
                $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
                try {
                    if ($method -eq "GET") {
                        $response = Invoke-WebRequest -Uri $url -Headers $headers -Method Get -UseBasicParsing
                    }
                    else {
                        $jsonBody = $body | ConvertTo-Json
                        $response = Invoke-WebRequest -Uri $url -Headers $headers -Method Post -Body $jsonBody -UseBasicParsing
                    }
                    $stopwatch.Stop()
                    
                    return @{
                        Success = $true
                        StatusCode = $response.StatusCode
                        ResponseTime = $stopwatch.ElapsedMilliseconds
                        CorrelationId = $response.Headers["X-Correlation-Id"]
                    }
                }
                catch {
                    $stopwatch.Stop()
                    return @{
                        Success = $false
                        StatusCode = 0
                        ResponseTime = $stopwatch.ElapsedMilliseconds
                        Error = $_.Exception.Message
                    }
                }
            } -ArgumentList "$ApiUrl$Endpoint", $headers, $Method, $Body
            
            $jobs += $job
        }
        
        # Wait a bit before next batch
        Start-Sleep -Milliseconds $delayMs
        
        # Clean up completed jobs
        $completedJobs = $jobs | Where-Object { $_.State -eq "Completed" }
        foreach ($job in $completedJobs) {
            $result = Receive-Job -Job $job
            $results.TotalRequests++
            
            if ($result.Success) {
                $results.SuccessfulRequests++
            }
            else {
                $results.FailedRequests++
            }
            
            $results.ResponseTimes += $result.ResponseTime
            Remove-Job -Job $job
        }
        
        $jobs = $jobs | Where-Object { $_.State -ne "Completed" }
    }
    
    # Wait for remaining jobs to complete
    Write-Host "Waiting for remaining requests to complete..." -ForegroundColor Yellow
    $jobs | Wait-Job | Out-Null
    
    foreach ($job in $jobs) {
        $result = Receive-Job -Job $job
        $results.TotalRequests++
        
        if ($result.Success) {
            $results.SuccessfulRequests++
        }
        else {
            $results.FailedRequests++
        }
        
        $results.ResponseTimes += $result.ResponseTime
        Remove-Job -Job $job
    }
    
    $results.EndTime = Get-Date
    
    # Calculate statistics
    $duration = ($results.EndTime - $results.StartTime).TotalSeconds
    $requestsPerSecond = [math]::Round($results.TotalRequests / $duration, 2)
    $avgResponseTime = [math]::Round(($results.ResponseTimes | Measure-Object -Average).Average, 2)
    $minResponseTime = ($results.ResponseTimes | Measure-Object -Minimum).Minimum
    $maxResponseTime = ($results.ResponseTimes | Measure-Object -Maximum).Maximum
    
    # Calculate percentiles
    $sortedTimes = $results.ResponseTimes | Sort-Object
    $p50Index = [math]::Floor($sortedTimes.Count * 0.50)
    $p95Index = [math]::Floor($sortedTimes.Count * 0.95)
    $p99Index = [math]::Floor($sortedTimes.Count * 0.99)
    
    $p50 = $sortedTimes[$p50Index]
    $p95 = $sortedTimes[$p95Index]
    $p99 = $sortedTimes[$p99Index]
    
    # Display results
    Write-Host "  Total Requests: " -NoNewline
    Write-Host $results.TotalRequests -ForegroundColor Green
    Write-Host "  Successful: " -NoNewline
    Write-Host $results.SuccessfulRequests -ForegroundColor Green
    Write-Host "  Failed: " -NoNewline
    Write-Host $results.FailedRequests -ForegroundColor $(if ($results.FailedRequests -eq 0) { "Green" } else { "Red" })
    Write-Host "  Requests/sec: " -NoNewline
    Write-Host $requestsPerSecond -ForegroundColor Green
    Write-Host "  Avg Response Time: " -NoNewline
    Write-Host "$avgResponseTime ms" -ForegroundColor Green
    Write-Host "  Min: " -NoNewline
    Write-Host "$minResponseTime ms" -ForegroundColor Green
    Write-Host "  Max: " -NoNewline
    Write-Host "$maxResponseTime ms" -ForegroundColor Green
    Write-Host "  p50: " -NoNewline
    Write-Host "$p50 ms" -ForegroundColor Green
    Write-Host "  p95: " -NoNewline
    Write-Host "$p95 ms" -ForegroundColor Green
    Write-Host "  p99: " -NoNewline
    Write-Host "$p99 ms" -ForegroundColor Green
    
    # Check if p99 meets requirement
    if ($p99 -lt 500) {
        Write-Host "  Status: " -NoNewline
        Write-Host "✓ PASS" -ForegroundColor Green -NoNewline
        Write-Host " (p99 < 500ms)"
    }
    else {
        Write-Host "  Status: " -NoNewline
        Write-Host "✗ FAIL" -ForegroundColor Red -NoNewline
        Write-Host " (p99 >= 500ms)"
    }
    
    Write-Host ""
    
    return $results
}

# Run load tests on various endpoints
Write-Host "========================================" -ForegroundColor Blue
Write-Host "Starting Load Tests" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue
Write-Host ""

$allResults = @{}

# Test 1: GET /api/companies
$allResults["companies"] = Run-LoadTest -Endpoint "/api/companies" -Method "GET" -Description "Get Companies List"

# Test 2: GET /api/users
$allResults["users"] = Run-LoadTest -Endpoint "/api/users" -Method "GET" -Description "Get Users List"

# Test 3: GET /api/roles
$allResults["roles"] = Run-LoadTest -Endpoint "/api/roles" -Method "GET" -Description "Get Roles List"

# Test 4: GET /api/currencies
$allResults["currencies"] = Run-LoadTest -Endpoint "/api/currencies" -Method "GET" -Description "Get Currencies List"

# Test 5: GET /api/branches
$allResults["branches"] = Run-LoadTest -Endpoint "/api/branches" -Method "GET" -Description "Get Branches List"

# Test 6: GET /api/auditlogs/legacy-view
$allResults["auditlogs"] = Run-LoadTest -Endpoint "/api/auditlogs/legacy-view?pageNumber=1&pageSize=20" -Method "GET" -Description "Get Audit Logs"

# Generate summary report
$summaryFile = Join-Path $ResultsDir "summary-$Timestamp.txt"

Write-Host "========================================" -ForegroundColor Blue
Write-Host "Generating Summary Report" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue
Write-Host ""

$summary = @"
Load Test Summary Report
========================
Date: $(Get-Date)
API URL: $ApiUrl
Target: $TargetRpm requests/minute ($TargetRps req/sec)
Concurrency: $Concurrency
Duration: $DurationSeconds seconds

Test Results:
-------------

"@

foreach ($key in $allResults.Keys) {
    $result = $allResults[$key]
    $duration = ($result.EndTime - $result.StartTime).TotalSeconds
    $rps = [math]::Round($result.TotalRequests / $duration, 2)
    $avgTime = [math]::Round(($result.ResponseTimes | Measure-Object -Average).Average, 2)
    
    $sortedTimes = $result.ResponseTimes | Sort-Object
    $p99Index = [math]::Floor($sortedTimes.Count * 0.99)
    $p99 = $sortedTimes[$p99Index]
    
    $summary += @"
Endpoint: $key
  Total Requests: $($result.TotalRequests)
  Successful: $($result.SuccessfulRequests)
  Failed: $($result.FailedRequests)
  Requests/sec: $rps
  Avg Response Time: $avgTime ms
  p99: $p99 ms

"@
}

$summary += @"

Performance Requirements Validation:
------------------------------------

Requirement 1: Support 10,000 requests per minute
  Target: $TargetRpm req/min ($TargetRps req/sec)
  Result: See individual endpoint results above
  
Requirement 2: Add no more than 10ms latency for 99% of operations
  Note: This requires instrumentation in the API to measure audit overhead specifically.
  Total p99 response time should be < 500ms (includes network + processing + audit)
  
Requirement 3: Use asynchronous writes
  Validation: Check application logs for async audit processing
  Expected: No blocking on audit writes

Recommendations:
----------------

1. Review individual endpoint results above
2. Check application logs for audit logging performance
3. Monitor database connection pool usage
4. Verify audit queue depth stays below 10,000
5. Check for any failed requests or errors

Next Steps:
-----------

1. If p99 > 500ms: Investigate slow endpoints and optimize
2. If failed requests > 1%: Check application logs for errors
3. If throughput < target: Increase concurrency or optimize bottlenecks
4. Run extended soak test (1+ hours) to validate stability
5. Use k6 for comprehensive load testing with 10,000 req/min sustained load

"@

$summary | Out-File -FilePath $summaryFile -Encoding UTF8

Write-Host "Summary report saved to: " -NoNewline
Write-Host $summaryFile -ForegroundColor Green
Write-Host ""

# Display summary
Write-Host $summary

Write-Host "========================================" -ForegroundColor Blue
Write-Host "Load Test Complete" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue
Write-Host ""
Write-Host "Results saved to: " -NoNewline
Write-Host $ResultsDir -ForegroundColor Green
Write-Host "Summary report: " -NoNewline
Write-Host $summaryFile -ForegroundColor Green
Write-Host ""
Write-Host "Note: For more comprehensive load testing with 10,000 req/min sustained load," -ForegroundColor Yellow
Write-Host "use the k6 script: k6 run load-test-10k-rpm.js" -ForegroundColor Yellow
Write-Host ""
