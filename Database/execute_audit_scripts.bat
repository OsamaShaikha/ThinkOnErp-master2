@echo off
REM =====================================================
REM Execute All Audit Scripts on Oracle Database
REM Database: THINKON_ERP/THINKON_ERP
REM Server: 178.104.126.99:1521/XEPDB1
REM =====================================================

echo.
echo =====================================================
echo ThinkOnERP - Audit System Scripts Execution
echo =====================================================
echo.
echo Database: THINKON_ERP
echo Server: 178.104.126.99:1521/XEPDB1
echo.
echo This will execute all audit-related SQL scripts.
echo.
pause

REM Change to Database directory
cd /d "%~dp0"

REM Execute SQL*Plus with the master script
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @EXECUTE_ALL_AUDIT_SCRIPTS.sql

echo.
echo =====================================================
echo Execution completed!
echo Check audit_scripts_execution.log for details
echo =====================================================
echo.
pause
