@echo off
REM =====================================================
REM Execute ALL Database Scripts - Master Setup
REM Database: THINKON_ERP/THINKON_ERP
REM Server: 178.104.126.99:1521/XEPDB1
REM =====================================================

echo.
echo =====================================================
echo ThinkOnERP - Complete Database Setup
echo =====================================================
echo.
echo Database: THINKON_ERP
echo Server: 178.104.126.99:1521/XEPDB1
echo.
echo This will execute ALL SQL scripts to build the
echo complete database from scratch.
echo.
echo WARNING: This may take several minutes!
echo.
pause

REM Change to Database directory
cd /d "%~dp0"

echo.
echo Starting execution...
echo.

REM Execute SQL*Plus with the master script
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @EXECUTE_ALL_SCRIPTS_MASTER.sql

echo.
echo =====================================================
echo Execution completed!
echo =====================================================
echo.
echo Check master_execution.log for detailed results
echo.
pause
