@echo off
REM =====================================================
REM Execute Additional Runtime Error Fixes
REM =====================================================

echo =====================================================
echo ThinkOnERP - Additional Runtime Error Fixes
echo =====================================================
echo.
echo This script will fix two additional runtime errors:
echo 1. Company repository column mismatch (HAS_LOGO)
echo 2. Legacy audit log date format error
echo.
echo Database: THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1
echo.
pause

echo.
echo Executing fixes...
echo.

REM Change to the Database directory where the script is located
cd /d "%~dp0"

sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_ADDITIONAL_RUNTIME_ERRORS.sql

echo.
echo =====================================================
echo Execution completed!
echo =====================================================
echo.
echo Please check the output above for any errors.
echo If successful, test the following:
echo 1. GET /api/companies - should return list of companies
echo 2. GET /api/auditlogs/legacy - should return legacy audit logs
echo.
pause
