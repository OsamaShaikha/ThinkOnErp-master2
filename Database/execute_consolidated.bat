@echo off
REM =====================================================
REM Execute Consolidated SQL File
REM Database: THINKON_ERP/THINKON_ERP
REM Server: 178.104.126.99:1521/XEPDB1
REM =====================================================

echo.
echo =====================================================
echo ThinkOnERP - Execute Consolidated SQL Script
echo =====================================================
echo.
echo Database: THINKON_ERP
echo Server: 178.104.126.99:1521/XEPDB1
echo.
echo This will execute ALL 69 SQL scripts from one file
echo File: ALL_SCRIPTS_CONSOLIDATED.sql
echo Size: 633 KB
echo.
echo WARNING: This may take 5-15 minutes!
echo.
pause

REM Change to Database directory
cd /d "%~dp0"

echo.
echo Starting execution...
echo.

REM Execute SQL*Plus with the consolidated script
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @ALL_SCRIPTS_CONSOLIDATED.sql

echo.
echo =====================================================
echo Execution completed!
echo =====================================================
echo.
echo Check consolidated_execution.log for detailed results
echo.
pause
