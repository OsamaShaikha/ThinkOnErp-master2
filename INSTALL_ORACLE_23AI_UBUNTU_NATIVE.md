# Install Oracle Database 23ai Free on Ubuntu 24.04 (Native Installation)

This guide walks you through installing Oracle Database 23ai Free directly on Ubuntu 24.04 without Docker.

**Why Oracle 23ai?** Native Ubuntu 24.04 support, modern features, free for production use, and no compatibility workarounds needed.

---

## Prerequisites

- Ubuntu 24.04 LTS
- At least 2GB RAM (4GB recommended)
- At least 12GB free disk space
- Root or sudo access
- Internet connection

---

## Step 1: Update System

```bash
sudo apt update
sudo apt upgrade -y
```

---

## Step 2: Install Required Dependencies

```bash
sudo apt install -y \
    wget \
    libaio1t64 \
    libaio-dev \
    unzip \
    bc \
    binutils \
    rlwrap
```

**Note for Ubuntu 24.04**: The package `libaio1` has been renamed to `libaio1t64` in Ubuntu 24.04.

---

## Step 3: Download Oracle Database 23ai Free

```bash
cd /tmp

# Download Oracle Database 23ai Free (no Oracle account required!)
wget https://download.oracle.com/otn-pub/otn_software/db-free/oracle-database-free-23ai-1.0-1.el8.x86_64.rpm
```

**Note**: Oracle 23ai Free is available without an Oracle account. If the link doesn't work, visit:
https://www.oracle.com/database/technologies/free-downloads.html

---

## Step 4: Install Oracle Database

### Option A: Convert to DEB (Recommended for Ubuntu 24.04)

**Note**: Ubuntu 24.04 requires using Alien to convert RPM packages. Direct RPM installation will fail with dependency errors.

```bash
cd /tmp

# Verify the RPM file exists and is complete (should be ~1.5GB)
ls -lh oracle-database-free-23ai-1.0-1.el8.x86_64.rpm

# Install alien
sudo apt install -y alien

# Convert RPM to DEB (this takes 5-10 minutes)
sudo alien --scripts oracle-database-free-23ai-1.0-1.el8.x86_64.rpm

# This creates: oracle-database-free-23ai_1.0-2_amd64.deb

# Install the DEB package
sudo dpkg -i oracle-database-free-23ai_1.0-2_amd64.deb

# Fix any dependency issues
sudo apt --fix-broken install
```

### Option B: Direct RPM Installation (Not Recommended)

**Warning**: This method will fail on Ubuntu 24.04 with dependency errors. Use Option A instead.

```bash
cd /tmp

# Install rpm package manager
sudo apt install -y rpm

# This will fail with dependency errors on Ubuntu 24.04
sudo rpm -ivh oracle-database-free-23ai-1.0-1.el8.x86_64.rpm
```

---

## Step 5: Configure Oracle Database

Run the configuration script:

```bash
sudo /etc/init.d/oracle-free-23ai configure
```

You'll be prompted to:
1. **Set SYS and SYSTEM passwords** (use a strong password, e.g., `OraclePass123`)
2. **Confirm the password**
3. **Choose whether to start database on boot** (Y recommended)

The configuration process takes 5-10 minutes.

---

## Step 6: Set Environment Variables

Add Oracle environment variables to your profile:

```bash
cat >> ~/.bashrc << 'EOF'

# Oracle Database 23ai Free Environment
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export ORACLE_SID=FREE
export ORACLE_BASE=/opt/oracle
export LD_LIBRARY_PATH=$ORACLE_HOME/lib:$LD_LIBRARY_PATH
export PATH=$ORACLE_HOME/bin:$PATH
export NLS_LANG=AMERICAN_AMERICA.AL32UTF8
EOF

# Reload the profile
source ~/.bashrc
```

**Verify environment variables**:

```bash
echo $ORACLE_HOME
echo $ORACLE_SID
```

---

## Step 7: Verify Installation

Check if Oracle is running:

```bash
sudo systemctl status oracle-free-23ai
```

If not running, start it:

```bash
sudo systemctl start oracle-free-23ai
sudo systemctl enable oracle-free-23ai
```

Check the listener:

```bash
lsnrctl status
```

---

## Step 8: Connect to Oracle Database

### Option 1: Using SQL*Plus

```bash
sqlplus sys as sysdba
# Enter the password you set during configuration
```

### Option 2: Using SQL*Plus with Password

```bash
sqlplus sys/OraclePass123@localhost:1521/FREE as sysdba
```

### Option 3: Using Easy Connect

```bash
sqlplus sys/OraclePass123@//localhost:1521/FREE as sysdba
```

---

## Step 9: Create ThinkOnErp Application User

Connect as SYSDBA and create the application user:

```sql
-- Connect as SYSDBA
sqlplus sys/OraclePass123@localhost:1521/FREE as sysdba

-- Create user
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
SELECT username, account_status FROM dba_users WHERE username = 'THINKONERP';

-- Exit
EXIT;
```

---

## Step 10: Test Connection with Application User

```bash
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE
```

If successful, you should see:

```
Connected to:
Oracle Database 23ai Free Release 23.0.0.0.0 - Develop, Learn, and Run for Free
Version 23.4.0.24.05

SQL>
```

---

## Step 11: Configure Firewall (if enabled)

```bash
# Allow Oracle listener port
sudo ufw allow 1521/tcp

# Check firewall status
sudo ufw status
```

---

## Step 12: Update ThinkOnErp Connection String

Update your `.env` file or connection string:

```bash
# For local Ubuntu installation with Oracle 23ai Free
ORACLE_CONNECTION_STRING="User Id=thinkonerp;Password=ThinkOnErp2024;Data Source=localhost:1521/FREE"

# Or using TNS format
ORACLE_CONNECTION_STRING="User Id=thinkonerp;Password=ThinkOnErp2024;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=FREE)))"
```

---

## Step 13: Run Database Scripts

Navigate to your ThinkOnErp project and run the database scripts:

```bash
cd ~/ThinkOnErp/Database/Scripts

# Connect and run scripts
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE @01_Create_Sequences.sql
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE @02_Create_SYS_ROLE_Procedures.sql
# ... continue with all scripts in order
```

Or use the master script:

```bash
cd ~/ThinkOnErp/Database
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE @EXECUTE_ALL_SCRIPTS_MASTER.sql
```

---

## Useful Commands

### Start/Stop Oracle Database

```bash
# Start
sudo systemctl start oracle-free-23ai

# Stop
sudo systemctl stop oracle-free-23ai

# Restart
sudo systemctl restart oracle-free-23ai

# Status
sudo systemctl status oracle-free-23ai
```

### Check Listener Status

```bash
lsnrctl status
```

### Start/Stop Listener

```bash
# Start
lsnrctl start

# Stop
lsnrctl stop
```

### Connect to Database

```bash
# As SYSDBA
sqlplus sys/OraclePass123@localhost:1521/FREE as sysdba

# As application user
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE
```

### View Database Information

```sql
-- Show database version
SELECT * FROM v$version;

-- Show database name
SELECT name FROM v$database;

-- Show tablespaces
SELECT tablespace_name FROM dba_tablespaces;

-- Show users
SELECT username, account_status FROM dba_users ORDER BY username;

-- Show Oracle 23ai new features
SELECT * FROM v$option WHERE parameter LIKE '%JSON%' OR parameter LIKE '%GRAPH%';
```

---

## Troubleshooting

### Issue 1: "ORA-12514: TNS:listener does not currently know of service"

**Solution**: Wait a few minutes after starting the database, or restart the listener:

```bash
lsnrctl stop
lsnrctl start

# Check listener status
lsnrctl status
```

### Issue 2: "ORA-01017: invalid username/password"

**Solution**: Reset the password:

```bash
sqlplus / as sysdba
ALTER USER thinkonerp IDENTIFIED BY ThinkOnErp2024;
EXIT;
```

### Issue 3: Database won't start

**Solution**: Check logs:

```bash
sudo tail -f /opt/oracle/diag/rdbms/free/FREE/trace/alert_FREE.log
```

### Issue 4: Insufficient memory

**Solution**: Oracle 23ai Free requires at least 2GB RAM. Check available memory:

```bash
free -h
```

### Issue 5: Port 1521 already in use

**Solution**: Check what's using the port:

```bash
sudo ss -tulpn | grep 1521
```

### Issue 6: Service not found

**Solution**: Manually start the database:

```bash
sudo su - oracle
sqlplus / as sysdba
STARTUP;
EXIT;
exit
```

---

## Uninstall Oracle Database (if needed)

```bash
# Stop the database
sudo systemctl stop oracle-free-23ai
sudo systemctl disable oracle-free-23ai

# Remove the package
sudo rpm -e oracle-database-free-23ai
# or if you used DEB
sudo dpkg -r oracle-database-free-23ai

# Remove Oracle directories
sudo rm -rf /opt/oracle
sudo rm -rf /etc/oratab
sudo rm -rf /etc/init.d/oracle-free-23ai

# Remove Oracle user (if created)
sudo userdel -r oracle
sudo groupdel oinstall
sudo groupdel dba
```

---

## Performance Tuning (Optional)

### Increase Memory Allocation

```sql
-- Connect as SYSDBA
sqlplus sys/OraclePass123@localhost:1521/FREE as sysdba

-- Check current SGA size
SHOW PARAMETER sga_target;

-- Increase SGA (example: 1GB)
ALTER SYSTEM SET sga_target=1G SCOPE=SPFILE;

-- Restart database
SHUTDOWN IMMEDIATE;
STARTUP;
EXIT;
```

### Enable Automatic Startup

```bash
sudo systemctl enable oracle-free-23ai
```

---

## Security Recommendations

1. **Change default passwords immediately**
2. **Restrict network access** (use firewall rules)
3. **Enable Oracle auditing**
4. **Regular backups** (use RMAN or Data Pump)
5. **Keep Oracle updated** with security patches

---

## Backup and Restore

### Export Database

```bash
# Export entire schema
expdp thinkonerp/ThinkOnErp2024@localhost:1521/FREE \
  schemas=thinkonerp \
  directory=DATA_PUMP_DIR \
  dumpfile=thinkonerp_backup.dmp \
  logfile=thinkonerp_backup.log
```

### Import Database

```bash
# Import schema
impdp thinkonerp/ThinkOnErp2024@localhost:1521/FREE \
  schemas=thinkonerp \
  directory=DATA_PUMP_DIR \
  dumpfile=thinkonerp_backup.dmp \
  logfile=thinkonerp_restore.log
```

---

## Oracle 23ai New Features

Oracle 23ai includes several modern features:

- **JSON Relational Duality**: Work with data as JSON or relational tables
- **Property Graphs**: Native graph database capabilities
- **JavaScript Stored Procedures**: Write procedures in JavaScript
- **Schema Annotations**: Add metadata to database objects
- **Automatic Partitioning**: Simplified partition management
- **Enhanced SQL**: New SQL functions and operators

Explore these features in the [Oracle 23ai documentation](https://docs.oracle.com/en/database/oracle/oracle-database/23/).

---

## Next Steps

1. ✅ Oracle Database 23ai Free installed and running
2. ✅ Application user created with proper privileges
3. ✅ Connection string configured
4. 🔄 Run ThinkOnErp database scripts (01-89)
5. 🔄 Test API connection to database
6. 🔄 Deploy ThinkOnErp API

---

## Additional Resources

- [Oracle Database 23ai Free Documentation](https://docs.oracle.com/en/database/oracle/oracle-database/23/)
- [Oracle SQL*Plus User's Guide](https://docs.oracle.com/en/database/oracle/oracle-database/23/sqpug/)
- [Oracle Database Installation Guide for Linux](https://docs.oracle.com/en/database/oracle/oracle-database/23/ladbi/)
- [Oracle 23ai New Features Guide](https://docs.oracle.com/en/database/oracle/oracle-database/23/nfcoa/)

---

**Installation Complete!** 🎉

Your Oracle Database 23ai Free is now running natively on Ubuntu 24.04 and ready for ThinkOnErp.

**Advantages of Oracle 23ai Free:**
- ✅ No license fees (free for production)
- ✅ Native Ubuntu 24.04 support
- ✅ Latest features and performance improvements
- ✅ Easier installation (no compatibility workarounds)
- ✅ Long-term support from Oracle
