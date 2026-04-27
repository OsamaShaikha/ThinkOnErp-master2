#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run the 1-hour sustained load test for the Full Traceability System.

.DESCRIPTION
    This script runs the sustained load test that validates the system can maintain
    10,000 requests per minute for 1 hour without performance degradation.

.PARAMETER ApiUrl
    The base URL of the ThinkOnErp API (default: http://localhost:5000)

.PARAMETER JwtToken
    Optional JWT token for authentication. If not provided, the script will
    attempt to authenticate using default superadmin credentials.

.PARAMETER OutputDir
    Directory to save test results (default: ./results)

.PARAMETER SaveResults
    Save detailed results to JSON file (default: true)

.EXAMPLE
    .\run-sustained-load-test.ps1

.EXAMPLE
    .\run-sustained-load-test.ps1 -ApiUrl "http://your-api:5000"

.EXAMPLE
    .\run-sustained-load-test.ps1 -JwtToken "your-jwt-token" -OutputDir "./test-results"
#>

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$JwtToken = "",
    [string]$OutputDir = "./results",
    [bool]$SaveResults = $true
)

# Color output functions
function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Cyan
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta
    Write-Host " $Message" -ForegroundColor Magenta
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta
    Write-Host ""
}

# Check if k6 is installed
Write-Header "Sustained Load Test - Pre-flight Checks"

Write-Info "Checking for k6 installation..."
$k6Version = k6 version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "k6 is not installed or not in PATH"
    Write-Info "Please install k6 from: https://k6.io/docs/getting-started/installation/"
    Write-Info ""
    Write-Info "Installation options:"
    Write-Info "  - Windows (Chocolatey): choco install k6"
    Write-Info "  - Windows (Scoop): scoop install k6"
    Write-Info "  - macOS: brew install k6"
    exit 1
}
Write-Success "k6 is installed: $k6Version"

# Check if API is accessible
Write-Info "Checking API accessibility at $ApiUrl..."
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/health" -Method Get -TimeoutSec 5 -ErrorAction SilentlyContinue
    Write-Success "API is accessible"
} catch {
    Write-Warning "Could not reach API health endpoint at $ApiUrl/health"
    Write-Warning "The API might not be running or the URL might be incorrect"
    Write-Info "Continuing anyway - k6 will handle connection errors..."
}

# Create output directory if it doesn't exist
if ($SaveResults) {
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
        Write-Success "Created output directory: $OutputDir"
    }
}

# Display test information
Write-Header "Sustained Load Test Configuration"

Write-Info "API URL: $ApiUrl"
Write-Info "JWT Token: $(if ($JwtToken) { '***PROVIDED***' } else { 'Will authenticate with default credentials' })"
Write-Info "Output Directory: $OutputDir"
Write-Info "Save Results: $SaveResults"
Write-Info ""
Write-Info "Test Parameters:"
Write-Info "  - Target Load: 10,000 requests/minute"
Write-Info "  - Sustained Duration: 60 minutes"
Write-Info "  - Total Duration: ~75 minutes"
Write-Info "  - Expected Requests: ~600,000"
Write-Info ""
Write-Warning "This test will run for approximately 75 minutes"
Write-Warning "Ensure you have sufficient database capacity for ~600,000 audit log entries"
Write-Info ""

# Confirm before starting
$confirmation = Read-Host "Do you want to proceed? (yes/no)"
if ($confirmation -ne "yes") {
    Write-Info "Test cancelled by user"
    exit 0
}

# Prepare k6 command
Write-Header "Starting Sustained Load Test"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$k6Args = @()

# Add environment variables
if ($ApiUrl) {
    $env:API_URL = $ApiUrl
}
if ($JwtToken) {
    $env:JWT_TOKEN = $JwtToken
}

# Add output file if requested
if ($SaveResults) {
    $outputFile = Join-Path $OutputDir "sustained-load-$timestamp.json"
    $logFile = Join-Path $OutputDir "sustained-load-$timestamp.log"
    $k6Args += "--out", "json=$outputFile"
    Write-Info "Results will be saved to: $outputFile"
    Write-Info "Console output will be saved to: $logFile"
}

# Add the test script
$k6Args += "sustained-load-test-1hour.js"

Write-Info "Starting k6 load test..."
Write-Info "Command: k6 run $($k6Args -join ' ')"
Write-Info ""
Write-Warning "Test is now running. Do not interrupt unless absolutely necessary."
Write-Info ""

# Run k6 and capture output
$startTime = Get-Date

if ($SaveResults) {
    k6 run @k6Args 2>&1 | Tee-Object -FilePath $logFile
} else {
    k6 run @k6Args
}

$exitCode = $LASTEXITCODE
$endTime = Get-Date
$duration = $endTime - $startTime

# Display results
Write-Header "Test Completed"

Write-Info "Test Duration: $($duration.ToString('hh\:mm\:ss'))"
Write-Info "End Time: $($endTime.ToString('yyyy-MM-dd HH:mm:ss'))"

if ($exitCode -eq 0) {
    Write-Success "Test completed successfully!"
    Write-Info ""
    Write-Info "Next Steps:"
    Write-Info "  1. Review the test results above"
    Write-Info "  2. Check for any threshold violations"
    Write-Info "  3. Analyze performance degradation metrics"
    Write-Info "  4. Verify database integrity"
    Write-Info "  5. Document results in task completion summary"
    
    if ($SaveResults) {
        Write-Info ""
        Write-Info "Results saved to:"
        Write-Info "  - JSON: $outputFile"
        Write-Info "  - Log: $logFile"
    }
} else {
    Write-Error "Test failed with exit code: $exitCode"
    Write-Info ""
    Write-Info "Troubleshooting:"
    Write-Info "  1. Check the error messages above"
    Write-Info "  2. Verify API is running and accessible"
    Write-Info "  3. Check database connectivity"
    Write-Info "  4. Review application logs for errors"
    Write-Info "  5. Consult SUSTAINED_LOAD_TEST_GUIDE.md for detailed troubleshooting"
    
    if ($SaveResults) {
        Write-Info ""
        Write-Info "Logs saved to: $logFile"
    }
}

Write-Info ""
Write-Info "For detailed analysis and troubleshooting, see:"
Write-Info "  - SUSTAINED_LOAD_TEST_GUIDE.md"
Write-Info "  - README.md"

exit $exitCode
