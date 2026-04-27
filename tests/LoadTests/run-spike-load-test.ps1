#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run spike load test for Full Traceability System (50,000 req/min burst)

.DESCRIPTION
    This script runs the spike load test that validates the system can handle
    sudden traffic bursts to 50,000 requests per minute and recover gracefully.

.PARAMETER ApiUrl
    The base URL of the ThinkOnErp API (default: http://localhost:5000)

.PARAMETER JwtToken
    Optional JWT token for authentication. If not provided, the script will
    attempt to authenticate using default superadmin credentials.

.PARAMETER OutputFile
    Optional output file for test results (JSON format)

.EXAMPLE
    .\run-spike-load-test.ps1

.EXAMPLE
    .\run-spike-load-test.ps1 -ApiUrl "http://your-api-url:port"

.EXAMPLE
    .\run-spike-load-test.ps1 -OutputFile "spike-results.json"
#>

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$JwtToken = "",
    [string]$OutputFile = ""
)

# Color output functions
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-ColorOutput "========================================" "Cyan"
    Write-ColorOutput $Message "Cyan"
    Write-ColorOutput "========================================" "Cyan"
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✓ $Message" "Green"
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠ $Message" "Yellow"
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "✗ $Message" "Red"
}

# Check if k6 is installed
Write-Header "Spike Load Test - Prerequisites Check"

Write-Host "Checking for k6 installation..."
$k6Installed = Get-Command k6 -ErrorAction SilentlyContinue

if (-not $k6Installed) {
    Write-Error "k6 is not installed or not in PATH"
    Write-Host ""
    Write-Host "Please install k6 from: https://k6.io/docs/getting-started/installation/"
    Write-Host ""
    Write-Host "Installation options:"
    Write-Host "  - Windows (Chocolatey): choco install k6"
    Write-Host "  - Windows (Scoop): scoop install k6"
    Write-Host "  - Windows (MSI): Download from https://dl.k6.io/msi/k6-latest-amd64.msi"
    exit 1
}

Write-Success "k6 is installed: $($k6Installed.Version)"

# Check if API is accessible
Write-Host ""
Write-Host "Checking API accessibility at $ApiUrl..."

try {
    $healthCheck = Invoke-WebRequest -Uri "$ApiUrl/api/monitoring/health" -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
    if ($healthCheck.StatusCode -eq 200) {
        Write-Success "API is accessible and healthy"
    } else {
        Write-Warning "API returned status code: $($healthCheck.StatusCode)"
    }
} catch {
    Write-Warning "Could not reach API health endpoint (this is optional)"
    Write-Host "  Attempting to continue with load test..."
}

# Display test configuration
Write-Header "Spike Load Test Configuration"

Write-Host "API URL:           $ApiUrl"
Write-Host "JWT Token:         $(if ($JwtToken) { '***PROVIDED***' } else { 'Will authenticate with default credentials' })"
Write-Host "Output File:       $(if ($OutputFile) { $OutputFile } else { 'Console only' })"
Write-Host ""
Write-Host "Test Profile:"
Write-Host "  - Baseline:      5 minutes at 10,000 req/min"
Write-Host "  - Spike:         3.5 minutes at 50,000 req/min"
Write-Host "  - Recovery:      5 minutes at 10,000 req/min"
Write-Host "  - Total Duration: ~17 minutes"
Write-Host ""
Write-Host "Expected Behavior:"
Write-Host "  ✓ System handles spike without crashing"
Write-Host "  ✓ Error rate during spike <5%"
Write-Host "  ✓ Queue backpressure prevents memory exhaustion"
Write-Host "  ✓ System recovers to normal performance after spike"

# Confirm before starting
Write-Host ""
$confirmation = Read-Host "Ready to start spike load test? (y/N)"
if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
    Write-Host "Test cancelled by user"
    exit 0
}

# Build k6 command
Write-Header "Starting Spike Load Test"

$k6Args = @("run")

# Add environment variables
if ($ApiUrl) {
    $k6Args += "--env"
    $k6Args += "API_URL=$ApiUrl"
}

if ($JwtToken) {
    $k6Args += "--env"
    $k6Args += "JWT_TOKEN=$JwtToken"
}

# Add output file if specified
if ($OutputFile) {
    $k6Args += "--out"
    $k6Args += "json=$OutputFile"
    Write-Host "Results will be saved to: $OutputFile"
}

# Add the test script
$k6Args += "spike-load-test-50k-rpm.js"

Write-Host ""
Write-Host "Executing: k6 $($k6Args -join ' ')"
Write-Host ""
Write-ColorOutput "⏱ Test starting... (this will take approximately 17 minutes)" "Yellow"
Write-Host ""

# Run the test
$testStartTime = Get-Date

try {
    & k6 $k6Args
    $exitCode = $LASTEXITCODE
} catch {
    Write-Error "Failed to execute k6: $_"
    exit 1
}

$testEndTime = Get-Date
$testDuration = $testEndTime - $testStartTime

# Display results summary
Write-Header "Spike Load Test Complete"

Write-Host "Test Duration: $($testDuration.ToString('mm\:ss'))"
Write-Host ""

if ($exitCode -eq 0) {
    Write-Success "All thresholds passed!"
    Write-Host ""
    Write-Host "Key Validations:"
    Write-Success "System handled spike load (50,000 req/min)"
    Write-Success "Error rate remained acceptable (<5%)"
    Write-Success "System recovered to normal performance"
    Write-Success "Queue backpressure mechanisms worked correctly"
} else {
    Write-Warning "Some thresholds failed (exit code: $exitCode)"
    Write-Host ""
    Write-Host "Review the test output above for details on which thresholds failed."
    Write-Host ""
    Write-Host "Common issues:"
    Write-Host "  - High error rate during spike (>5%)"
    Write-Host "  - System did not recover after spike"
    Write-Host "  - Queue depth exceeded memory limits"
    Write-Host "  - Database connection pool exhaustion"
}

Write-Host ""
Write-Host "Next Steps:"
Write-Host "  1. Review the detailed metrics above"
Write-Host "  2. Check application logs for errors or warnings"
Write-Host "  3. Monitor database performance during spike"
Write-Host "  4. Verify queue depth remained manageable"
Write-Host "  5. Document results in task completion summary"

if ($OutputFile) {
    Write-Host ""
    Write-Host "Detailed results saved to: $OutputFile"
}

Write-Host ""
exit $exitCode
