# Install Oracle Database on Ubuntu (Native Installation)

This guide walks you through installing Oracle Database 19c directly on Ubuntu without Docker.

---

## Prerequisites

- Ubuntu 24.04 LTS (also compatible with 20.04 and 22.04)
- At least 4GB RAM (8GB recommended for Oracle 19c)
- At least 20GB free disk space
- Root or sudo access

**Note for Ubuntu 24.04**: Some additional compatibility steps are required due to newer system libraries.

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
    alien \
    libaio1 \
    libaio-dev \
    unixodbc \
    unixodbc-dev \
    wget \
    bc \
    binutils \
    rlwrap \
    rpm \
    ksh \
    make \
    gcc \
    build-essential \
    libnsl2 \
    libnsl-dev
```

**Ubuntu 24.04 Specific**: Install additional compatibility libraries:

```bash
# Create symbolic links for compatibility
sudo ln -sf /usr/lib/x86_64-linux-gnu/libnsl.so.2 /usr/lib/x86_64-linux-gnu/libnsl.so.1
sudo ln -sf /usr/lib/x86_64-linux-gnu/libtinfo.so.6 /usr/lib/x86_64-linux-gnu/libtinfo.so.5
```

---

## Step 3: Download Oracle Database 19c

Visit Oracle's official website to download Oracle Database 19c:

```bash
cd /tmp

# Download Oracle Database 19c RPM (requires Oracle account)
# Option 1: Download from Oracle website manually
# https://www.oracle.com/database/technologies/oracle-database-software-downloads.html#19c

# Option 2: If you have the direct link (requires Oracle account login)
# For Oracle Linux 7:
wget https://download.oracle.com/otn/linux/oracle19c/190000/oracle-database-ee-19c-1.0-1.x86_64.rpm

# OR for Oracle Linux 8:
# wget https://download.oracle.com/otn/linux/oracle19c/190000/oracle-database-ee-19c-1.0-1.ol8.x86_64.rpm
```

**Note**: You'll need an Oracle account to download. If the direct link doesn't work, download manually from:
https://www.oracle.com/database/technologies/oracle-database-software-downloads.html#19c

**Important**: Oracle 19c comes in different editions:
- **Enterprise Edition (EE)** - Full features (recommended for production)
- **Standard Edition 2 (SE2)** - Limited features
- **Express Edition (XE)** - Free but limited (not available for 19c, only 18c and 21c)

For this guide, we'll use **Enterprise Edition** for development/testing.

---

## Step 4: Convert RPM to DEB Package

```bash
cd /tmp
sudo alien --scripts oracle-database-ee-19c-1.0-1.x86_64.rpm
```

This will create a `.deb` file (may take 10-15 minutes for Oracle 19c).

---

## Step 5: Install Oracle Database

```bash
sudo dpkg -i oracle-database-ee-19c_1.0-2_amd64.deb
```

If you encounter dependency errors:

```bash
sudo apt --fix-broken install
sudo dpkg -i oracle-database-ee-19c_1.0-2_amd64.deb
```

---

## Step 6: Configure Oracle Database

Run the configuration script:

```bash
sudo /etc/init.d/oracle-database-ee-19c configure
```

**If the script doesn't exist**, manually configure:

```bash
# Set Oracle environment
export ORACLE_HOME=/opt/oracle/product/19c/dbhome_1
export ORACLE_SID=ORCL
export PATH=$ORACLE_HOME/bin:$PATH

# Run database configuration assistant
sudo -u oracle $ORACLE_HOME/bin/dbca -silent \
  -createDatabase \
  -templateName General_Purpose.dbc \
  -gdbname ORCL \
  -sid ORCL \
  -responseFile NO_VALUE \
  -characterSet AL32UTF8 \
  -sysPassword OraclePass123 \
  -systemPassword OraclePass123 \
  -createAsContainerDatabase false \
  -databaseType MULTIPURPOSE \
  -automaticMemoryManagement false \
  -storageType FS \
  -datafileDestination /opt/oracle/oradata \
  -redoLogFileSize 50 \
  -emConfiguration NONE \
  -ignorePreReqs
```

You'll be prompted to:
1. **Set SYS and SYSTEM passwords** (use a strong password, e.g., `OraclePass123`)
2. **Confirm the password**
3. **Choose whether to start database on boot** (Y recommended)

The configuration process takes 10-20 minutes for Oracle 19c.

**Ubuntu 24.04 Note**: If you encounter systemd service errors, you may need to manually create the service file (see troubleshooting section).

---

## Step 7: Set Environment Variables

Add Oracle environment variables to your profile:

```bash
cat >> ~/.bashrc << 'EOF'

# Oracle Database 19c Environment
export ORACLE_HOME=/opt/oracle/product/19c/dbhome_1
export ORACLE_SID=ORCL
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

## Step 8: Verify Installation

Check if Oracle is running:

```bash
sudo systemctl status oracle-database-ee-19c
```

If the service doesn't exist (common on Ubuntu 24.04), check manually:

```bash
ps -ef | grep pmon
```

If not running, start it manually:

```bash
# Switch to oracle user
sudo su - oracle

# Start listener
lsnrctl start

# Start database
sqlplus / as sysdba
STARTUP;
EXIT;

# Exit oracle user
exit
```

**Create systemd service for Ubuntu 24.04** (if needed):

```bash
sudo tee /etc/systemd/system/oracle-database-19c.service > /dev/null <<'EOF'
[Unit]
Description=Oracle Database 19c
After=network.target

[Service]
Type=forking
User=oracle
Group=oinstall
Environment="ORACLE_HOME=/opt/oracle/product/19c/dbhome_1"
Environment="ORACLE_SID=ORCL"
ExecStart=/opt/oracle/product/19c/dbhome_1/bin/dbstart /opt/oracle/product/19c/dbhome_1
ExecStop=/opt/oracle/product/19c/dbhome_1/bin/dbshut /opt/oracle/product/19c/dbhome_1
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF

# Enable and start the service
sudo systemctl daemon-reload
sudo systemctl enable oracle-database-19c
sudo systemctl start oracle-database-19c
```

---

## Step 9: Connect to Oracle Database

### Option 1: Using SQL*Plus

```bash
sqlplus sys as sysdba
# Enter the password you set during configuration
```

### Option 2: Using SQL*Plus with Password

```bash
sqlplus sys/OraclePass123@localhost:1521/ORCL as sysdba
```

### Option 3: Using Easy Connect

```bash
sqlplus sys/OraclePass123@//localhost:1521/ORCL as sysdba
```

---

## Step 10: Create ThinkOnErp Application User

Connect as SYSDBA and create the application user:

```sql
-- Connect as SYSDBA
sqlplus sys/OraclePass123@localhost:1521/ORCL as sysdba

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

## Step 11: Test Connection with Application User

```bash
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/ORCL
```

If successful, you should see:

```
Connected to:
Oracle Database 19c Enterprise Edition Release 19.0.0.0.0 - Production
Version 19.3.0.0.0

SQL>
```

---

## Step 12: Configure Firewall (if enabled)

```bash
# Allow Oracle listener port
sudo ufw allow 1521/tcp

# Check firewall status
sudo ufw status
```

---

## Step 13: Update ThinkOnErp Connection String

Update your `.env` file or connection string:

```bash
# For local Ubuntu installation with Oracle 19c
ORACLE_CONNECTION_STRING="User Id=thinkonerp;Password=ThinkOnErp2024;Data Source=localhost:1521/ORCL"

# Or using TNS format
ORACLE_CONNECTION_STRING="User Id=thinkonerp;Password=ThinkOnErp2024;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCL)))"
```

---

## Step 14: Run Database Scripts

Navigate to your ThinkOnErp project and run the database scripts:

```bash
cd ~/ThinkOnErp/Database/Scripts

# Connect and run scripts
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/ORCL @01_Create_Sequences.sql
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/ORCL @02_Create_SYS_ROLE_Procedures.sql
# ... continue with all scripts in order
```

Or use the master script:

```bash
cd ~/ThinkOnErp/Database
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/ORCL @EXECUTE_ALL_SCRIPTS_MASTER.sql
```

---

## Useful Commands

### Start/Stop Oracle Database

```bash
# Using systemd (if service is configured)
sudo systemctl start oracle-database-19c
sudo systemctl stop oracle-database-19c
sudo systemctl restart oracle-database-19c
sudo systemctl status oracle-database-19c

# Manual start/stop
sudo su - oracle
sqlplus / as sysdba
STARTUP;
EXIT;

# To stop
sqlplus / as sysdba
SHUTDOWN IMMEDIATE;
EXIT;
exit
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
sqlplus sys/OraclePass123@localhost:1521/ORCL as sysdba

# As application user
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/ORCL
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
sudo tail -f /opt/oracle/diag/rdbms/orcl/ORCL/trace/alert_ORCL.log
```

### Issue 4: Insufficient memory

**Solution**: Oracle 19c requires at least 4GB RAM. Check available memory:

```bash
free -h
```

### Issue 5: Port 1521 already in use

**Solution**: Check what's using the port:

```bash
sudo netstat -tulpn | grep 1521
# or
sudo ss -tulpn | grep 1521
```

### Issue 6: Ubuntu 24.04 - Missing libnsl.so.1

**Solution**: Create symbolic link:

```bash
sudo ln -sf /usr/lib/x86_64-linux-gnu/libnsl.so.2 /usr/lib/x86_64-linux-gnu/libnsl.so.1
```

### Issue 7: Ubuntu 24.04 - systemd service not found

**Solution**: Use the manual systemd service creation from Step 8, or start database manually:

```bash
sudo su - oracle
lsnrctl start
sqlplus / as sysdba
STARTUP;
EXIT;
exit
```

---

## Uninstall Oracle Database (if needed)

```bash
# Stop the database
sudo systemctl stop oracle-database-19c
sudo systemctl disable oracle-database-19c

# Or stop manually
sudo su - oracle
lsnrctl stop
sqlplus / as sysdba
SHUTDOWN IMMEDIATE;
EXIT;
exit

# Remove the package
sudo dpkg -r oracle-database-ee-19c

# Remove Oracle directories
sudo rm -rf /opt/oracle
sudo rm -rf /etc/oratab
sudo rm -rf /etc/systemd/system/oracle-database-19c.service

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
sqlplus sys/OraclePass123@localhost:1521/XE as sysdba

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
sudo systemctl enable oracle-xe-21c
```

---

## Security Recommendations

1. **Change default passwords immediately**
2. **Restrict network access** (use firewall rules)
3. **Enable Oracle auditing**
4. **Regular backups** (use RMAN or export/import)
5. **Keep Oracle updated** with security patches

---

## Backup and Restore

### Export Database

```bash
# Export entire schema
expdp thinkonerp/ThinkOnErp2024@localhost:1521/XE \
  schemas=thinkonerp \
  directory=DATA_PUMP_DIR \
  dumpfile=thinkonerp_backup.dmp \
  logfile=thinkonerp_backup.log
```

### Import Database

```bash
# Import schema
impdp thinkonerp/ThinkOnErp2024@localhost:1521/XE \
  schemas=thinkonerp \
  directory=DATA_PUMP_DIR \
  dumpfile=thinkonerp_backup.dmp \
  logfile=thinkonerp_restore.log
```

---

## Next Steps

1. ✅ Oracle Database installed and running
2. ✅ Application user created with proper privileges
3. ✅ Connection string configured
4. 🔄 Run ThinkOnErp database scripts (01-89)
5. 🔄 Test API connection to database
6. 🔄 Deploy ThinkOnErp API

---

## Additional Resources

- [Oracle Database 21c XE Documentation](https://docs.oracle.com/en/database/oracle/oracle-database/21/xeinl/)
- [Oracle SQL*Plus User's Guide](https://docs.oracle.com/en/database/oracle/oracle-database/21/sqpug/)
- [Oracle Database Installation Guide for Linux](https://docs.oracle.com/en/database/oracle/oracle-database/21/ladbi/)

---

**Installation Complete!** 🎉

Your Oracle Database is now running natively on Ubuntu and ready for ThinkOnErp.
