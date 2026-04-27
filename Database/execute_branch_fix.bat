@echo off
REM =====================================================
REM Execute Branch Procedures Fix
REM =====================================================

echo =====================================================
echo Executing Branch Procedures Fix
echo =====================================================
echo.
echo Database: THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1
echo Script: FIX_BRANCH_PROCEDURES.sql
echo.

sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_BRANCH_PROCEDURES.sql

echo.
echo =====================================================
echo Execution completed!
echo =====================================================
echo.
pause
