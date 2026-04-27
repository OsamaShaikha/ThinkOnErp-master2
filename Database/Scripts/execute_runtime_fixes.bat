@echo off
REM =====================================================
REM Execute Runtime Error Fixes
REM =====================================================

echo =====================================================
echo ThinkOnERP - Runtime Error Fixes
echo =====================================================
echo.
echo This script will fix two critical runtime errors:
echo 1. Missing stored procedure for SLA escalation
echo 2. Check constraint violation on audit logging
echo.
echo Database: THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1
echo.
pause

echo.
echo Executing fixes...
echo.

sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_RUNTIME_ERRORS.sql

echo.
echo =====================================================
echo Execution completed!
echo =====================================================
echo.
echo Please check the output above for any errors.
echo If successful, restart your application.
echo.
pause
