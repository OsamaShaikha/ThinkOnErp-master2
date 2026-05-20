#!/bin/bash
# ThinkOnErp Oracle Database Initialization Script
# This script runs automatically when the Oracle container starts for the first time

set -e

echo "=========================================="
echo "ThinkOnErp Database Initialization"
echo "=========================================="

# Wait for Oracle to be ready
echo "Waiting for Oracle Database to be ready..."
until sqlplus -s sys/${ORACLE_PWD}@//localhost:1521/XE as sysdba <<EOF
SELECT 'Database is ready' FROM DUAL;
EXIT;
EOF
do
  echo "Waiting for database..."
  sleep 5
done

echo "Oracle Database is ready!"

# Create the THINKON_ERP2 user and schema
echo "Creating THINKON_ERP2 user and schema..."
sqlplus -s sys/${ORACLE_PWD}@//localhost:1521/XE as sysdba <<EOF
-- Create user if not exists
DECLARE
  user_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO user_count FROM dba_users WHERE username = 'THINKON_ERP2';
  IF user_count = 0 THEN
    EXECUTE IMMEDIATE 'CREATE USER THINKON_ERP2 IDENTIFIED BY THINKON_ERP2';
    EXECUTE IMMEDIATE 'GRANT CONNECT, RESOURCE, DBA TO THINKON_ERP2';
    EXECUTE IMMEDIATE 'GRANT UNLIMITED TABLESPACE TO THINKON_ERP2';
    DBMS_OUTPUT.PUT_LINE('User THINKON_ERP2 created successfully');
  ELSE
    DBMS_OUTPUT.PUT_LINE('User THINKON_ERP2 already exists');
  END IF;
END;
/
EXIT;
EOF

# Switch to XEPDB1 and run the consolidated script
echo "Running consolidated database scripts..."
sqlplus THINKON_ERP/THINKON_ERP@//localhost:1521/XEPDB1 <<EOF
SET SERVEROUTPUT ON SIZE UNLIMITED
SET ECHO ON
SET FEEDBACK ON
SET VERIFY OFF

SPOOL /tmp/thinkonerp_init.log

@/opt/oracle/scripts/setup/ALL_SCRIPTS_CONSOLIDATED.sql

SPOOL OFF

EXIT;
EOF

echo "=========================================="
echo "Database initialization completed!"
echo "=========================================="
echo "User: THINKON_ERP2"
echo "Password: THINKON_ERP2"
echo "Connection: localhost:1521/XEPDB1"
echo "=========================================="
