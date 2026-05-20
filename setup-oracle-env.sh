#!/bin/bash
# Setup Oracle 23ai Free Environment and Test Connection

echo "=========================================="
echo "Oracle 23ai Free - Environment Setup"
echo "=========================================="
echo ""

# Step 1: Set environment variables for oracle user
echo "Step 1: Setting up Oracle environment variables..."
sudo su - oracle << 'ORACLE_ENV'
cat >> ~/.bashrc << 'EOF'

# Oracle Database 23ai Free Environment
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export ORACLE_SID=FREE
export ORACLE_BASE=/opt/oracle
export LD_LIBRARY_PATH=$ORACLE_HOME/lib:$LD_LIBRARY_PATH
export PATH=$ORACLE_HOME/bin:$PATH
export NLS_LANG=AMERICAN_AMERICA.AL32UTF8
EOF

source ~/.bashrc
echo "Oracle environment variables set:"
echo "ORACLE_HOME=$ORACLE_HOME"
echo "ORACLE_SID=$ORACLE_SID"
echo "PATH=$PATH"
ORACLE_ENV

echo "✅ Oracle user environment configured"
echo ""

# Step 2: Check listener status
echo "Step 2: Checking listener status..."
sudo su - oracle -c "source ~/.bashrc && lsnrctl status"
echo ""

# Step 3: Test database connection
echo "Step 3: Testing database connection..."
echo "Enter SYS password when prompted (or press Ctrl+C to skip)"
sudo su - oracle -c "source ~/.bashrc && sqlplus / as sysdba" << 'SQL'
SELECT instance_name, status, version FROM v\$instance;
SELECT name FROM v\$database;
EXIT;
SQL

echo ""
echo "=========================================="
echo "Next Steps:"
echo "=========================================="
echo "1. Set SYS password (if not already set):"
echo "   sudo su - oracle"
echo "   sqlplus / as sysdba"
echo "   ALTER USER sys IDENTIFIED BY YourPassword;"
echo ""
echo "2. Create ThinkOnErp user:"
echo "   Run the commands from INSTALL_ORACLE_23AI_UBUNTU_NATIVE.md Step 9"
echo ""
echo "3. Update .env file with connection string"
echo ""
