# =====================================================
# Fix Branch Procedures - PowerShell Script
# =====================================================
# This script fixes the Branch API procedures by adding missing columns
# =====================================================

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Fix Branch Procedures" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Database connection details
$dbUser = "THINKON_ERP"
$dbPassword = "THINKON_ERP"
$dbHost = "178.104.126.99"
$dbPort = "1521"
$dbService = "XEPDB1"
$connectionString = "${dbUser}/${dbPassword}@${dbHost}:${dbPort}/${dbService}"

Write-Host "Database: $connectionString" -ForegroundColor Yellow
Write-Host ""

# Check if sqlplus is available
$sqlplusPath = Get-Command sqlplus -ErrorAction SilentlyContinue

if (-not $sqlplusPath) {
    Write-Host "ERROR: sqlplus not found in PATH" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please do one of the following:" -ForegroundColor Yellow
    Write-Host "1. Add Oracle Client to your PATH" -ForegroundColor Yellow
    Write-Host "2. Use SQL Developer to execute: Database/FIX_BRANCH_PROCEDURES.sql" -ForegroundColor Yellow
    Write-Host "3. Manually execute the SQL script in SQL*Plus" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "See FIX_APPLICATION_CRASH.md for detailed instructions" -ForegroundColor Yellow
    Write-Host ""
    
    # Ask if user wants to open the SQL file
    $response = Read-Host "Would you like to open the SQL file now? (Y/N)"
    if ($response -eq 'Y' -or $response -eq 'y') {
        Start-Process "Database\FIX_BRANCH_PROCEDURES.sql"
    }
    
    pause
    exit 1
}

Write-Host "✓ sqlplus found: $($sqlplusPath.Source)" -ForegroundColor Green
Write-Host ""

# Execute the fix script
Write-Host "Executing fix script..." -ForegroundColor Yellow
Write-Host ""

try {
    $scriptPath = Join-Path $PSScriptRoot "Database\FIX_BRANCH_PROCEDURES.sql"
    
    if (-not (Test-Path $scriptPath)) {
        Write-Host "ERROR: Script not found: $scriptPath" -ForegroundColor Red
        pause
        exit 1
    }
    
    # Execute the SQL script
    $output = & sqlplus -S $connectionString "@$scriptPath" 2>&1
    
    Write-Host $output
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "=====================================================" -ForegroundColor Green
        Write-Host "✓ Fix completed successfully!" -ForegroundColor Green
        Write-Host "=====================================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Yellow
        Write-Host "1. Restart the ThinkOnERP application" -ForegroundColor Yellow
        Write-Host "2. Navigate to https://localhost:7136/swagger" -ForegroundColor Yellow
        Write-Host "3. Test the Branch API: GET /api/branches" -ForegroundColor Yellow
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "=====================================================" -ForegroundColor Red
        Write-Host "✗ Fix failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        Write-Host "=====================================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please check the error messages above" -ForegroundColor Yellow
        Write-Host "See FIX_APPLICATION_CRASH.md for troubleshooting" -ForegroundColor Yellow
        Write-Host ""
    }
} catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "See FIX_APPLICATION_CRASH.md for alternative methods" -ForegroundColor Yellow
    Write-Host ""
}

pause
