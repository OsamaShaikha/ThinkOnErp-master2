#!/usr/bin/env pwsh
# =====================================================
# Execute ALL Database Scripts - Master Setup
# Database: THINKON_ERP/THINKON_ERP
# Server: 178.104.126.99:1521/XEPDB1
# =====================================================

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "ThinkOnERP - Complete Database Setup" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database: THINKON_ERP" -ForegroundColor Yellow
Write-Host "Server: 178.104.126.99:1521/XEPDB1" -ForegroundColor Yellow
Write-Host ""
Write-Host "This will execute ALL SQL scripts to build the" -ForegroundColor White
Write-Host "complete database from scratch." -ForegroundColor White
Write-Host ""
Write-Host "WARNING: This may take several minutes!" -ForegroundColor Red
Write-Host ""

# Prompt for password (more secure)
$password = Read-Host "Enter password for THINKON_ERP user" -AsSecureString
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
$plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

Write-Host ""
Write-Host "Connecting to database..." -ForegroundColor Green
Write-Host "This will take several minutes. Please wait..." -ForegroundColor Yellow
Write-Host ""

# Change to Database directory
Set-Location $PSScriptRoot

# Execute SQL*Plus with the master script
$env:NLS_LANG = "AMERICAN_AMERICA.AL32UTF8"
sqlplus "THINKON_ERP/$plainPassword@178.104.126.99:1521/XEPDB1" @EXECUTE_ALL_SCRIPTS_MASTER.sql

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Execution completed!" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Check master_execution.log for detailed results" -ForegroundColor Yellow
Write-Host ""

# Clear password from memory
$plainPassword = $null
[System.GC]::Collect()

Read-Host "Press Enter to exit"
