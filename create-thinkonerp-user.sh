#!/bin/bash
# Create ThinkOnErp Database User in Oracle 23ai Free

echo "=========================================="
echo "Create ThinkOnErp Database User"
echo "=========================================="
echo ""

# Check if database is running
if ! ps aux | grep -q "[d]b_pmon_FREE"; then
    echo "❌ Oracle Database is not running!"
    echo "Start it with: sudo /etc/init.d/oracle-free-23ai start"
    exit 1
fi

echo "✅ Oracle Database is running"
echo ""

# Check and start listener if not running
echo "Checking Oracle listener..."
if ! sudo ss -tulpn | grep -q ":1521"; then
    echo "⚠️  Listener is not running. Starting listener..."
    sudo su - oracle << 'START_LISTENER'
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export PATH=$ORACLE_HOME/bin:$PATH
$ORACLE_HOME/bin/lsnrctl start
START_LISTENER
    echo "✅ Listener started"
else
    echo "✅ Listener is already running"
fi

echo ""

# Set environment and create user
echo "Creating ThinkOnErp user..."
echo ""

sudo su - oracle << 'ORACLE_SETUP'
# Set environment
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export ORACLE_SID=FREE
export PATH=$ORACLE_HOME/bin:$PATH

# Connect and create user
$ORACLE_HOME/bin/sqlplus / as sysdba << 'SQL'
-- Show current database info
SELECT instance_name, status FROM v$instance;
SELECT name FROM v$database;

-- Check if user already exists
SELECT username, account_status FROM dba_users WHERE username = 'THINKONERP';

-- Create user (will fail if already exists - that's OK)
CREATE USER thinkonerp IDENTIFIED BY ThinkOnErp2024;

-- Grant privileges
GRANT CONNECT, RESOURCE, DBA TO thinkonerp;
GRANT CREATE SESSION TO thinkonerp;
GRANT CREATE TABLE TO thinkonerp;
GRANT CREATE VIEW TO thinkonerp;
GRANT CREATE SEQUENCE TO thinkonerp;
GRANT CREATE PROCEDURE TO thinkonerp;
GRANT CREATE TRIGGER TO thinkonerp;
GRANT UNLIMITED TABLESPACE TO thinkonerp;

-- Verify user creation
SELECT username, account_status, created FROM dba_users WHERE username = 'THINKONERP';

-- Show granted roles
SELECT grantee, granted_role FROM dba_role_privs WHERE grantee = 'THINKONERP';

EXIT;
SQL
ORACLE_SETUP

echo ""
echo "=========================================="
echo "Testing ThinkOnErp User Connection"
echo "=========================================="
echo ""

# Test connection with thinkonerp user
sudo su - oracle << 'TEST_CONNECTION'
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export ORACLE_SID=FREE
export PATH=$ORACLE_HOME/bin:$PATH

$ORACLE_HOME/bin/sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE << 'SQL'
SELECT 'Connection successful!' as status FROM dual;
SELECT username, account_status FROM user_users;
EXIT;
SQL
TEST_CONNECTION

echo ""
echo "=========================================="
echo "✅ Setup Complete!"
echo "=========================================="
echo ""
echo "ThinkOnErp user credentials:"
echo "  Username: thinkonerp"
echo "  Password: ThinkOnErp2024"
echo "  Connection: localhost:1521/FREE"
echo ""
echo "Connection string for .env file:"
echo 'ORACLE_CONNECTION_STRING="User Id=thinkonerp;Password=ThinkOnErp2024;Data Source=localhost:1521/FREE"'
echo ""
echo "Next steps:"
echo "1. Update your .env file with the connection string above"
echo "2. Run database scripts: cd Database && ./init-database.sh"
echo "3. Test API connection"
echo ""
