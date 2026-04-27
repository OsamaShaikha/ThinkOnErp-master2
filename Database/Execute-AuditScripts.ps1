#!/usr/bin/env pwsh
# =====================================================
# Execute All Audit Scripts on Oracle Database
# Database: THINKON_ERP/THINKON_ERP
# Server: 178.104.126.99:1521/XEPDB1
# =====================================================

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "ThinkOnERP - Audit System Scripts Execution" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database: THINKON_ERP" -ForegroundColor Yellow
Write-Host "Server: 178.104.126.99:1521/XEPDB1" -ForegroundColor Yellow
Write-Host ""
Write-Host "This will execute all audit-related SQL scripts." -ForegroundColor White
Write-Host ""

# Prompt for password (more secure than hardcoding)
$password = Read-Host "Enter password for THINKON_ERP user" -AsSecureString
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
$plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

Write-Host ""
Write-Host "Connecting to database..." -ForegroundColor Green

# Change to Database directory
Set-Location $PSScriptRoot

# Execute SQL*Plus with the master script
$connectionString = "$plainPassword@178.104.126.99:1521/XEPDB1"
echo "THINKON_ERP/$connectionString" | sqlplus /nolog @EXECUTE_ALL_AUDIT_SCRIPTS.sql

# Alternative method using direct connection
# sqlplus "THINKON_ERP/$plainPassword@178.104.126.99:1521/XEPDB1" @EXECUTE_ALL_AUDIT_SCRIPTS.sql

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Execution completed!" -ForegroundColor Green
Write-Host "Check audit_scripts_execution.log for details" -ForegroundColor Yellow
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Clear password from memory
$plainPassword = $null
[System.GC]::Collect()

Read-Host "Press Enter to exit"
