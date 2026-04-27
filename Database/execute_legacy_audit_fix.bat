@echo off
REM =====================================================
REM Execute Legacy Audit Column Fix
REM =====================================================
REM This script fixes the column mismatch in SP_SYS_AUDIT_LOG_LEGACY_SELECT
REM =====================================================

echo =====================================================
echo Executing Legacy Audit Column Fix
echo =====================================================
echo.
echo Database: THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1
echo Script: FIX_LEGACY_AUDIT_COLUMNS.sql
echo.
echo Press any key to continue or Ctrl+C to cancel...
pause > nul

echo.
echo Executing fix...
echo.

sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_LEGACY_AUDIT_COLUMNS.sql

echo.
echo =====================================================
echo Execution completed!
echo =====================================================
echo.
echo Please check the output above for any errors.
echo If the procedure shows STATUS = VALID, the fix was successful.
echo.
pause
