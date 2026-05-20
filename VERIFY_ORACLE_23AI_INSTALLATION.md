# Verify Oracle 23ai Free Installation on Ubuntu 24.04

## Current Status

✅ Oracle Database 23ai Free package installed via DEB  
⚠️ Configuration script reports: "Oracle Database instance FREE already configured"  
❌ systemd service not found (expected with Alien conversion)

---

## Step 1: Check if Oracle Database is Running

Run these commands to check if Oracle processes are active:

```bash
# Check for Oracle processes
ps aux | grep pmon

# Check for Oracle listener
ps aux | grep tnslsnr

# Check if port 1521 is listening
sudo ss -tulpn | grep 1521
```

**Expected output if running:**
- You should see `ora_pmon_FREE` process
- You should see `tnslsnr` process
- Port 1521 should be in LISTEN state

---

## Step 2: Switch to Oracle User

The Oracle installation creates an `oracle` user. Switch to it:

```bash
# Switch to oracle user
sudo su - oracle

# Check environment
echo $ORACLE_HOME
echo $ORACLE_SID
```

**If environment variables are not set**, set them:

```bash
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export ORACLE_SID=FREE
export ORACLE_BASE=/opt/oracle
export PATH=$ORACLE_HOME/bin:$PATH
export LD_LIBRARY_PATH=$ORACLE_HOME/lib:$LD_LIBRARY_PATH
```

---

## Step 3: Check Database Status

As the `oracle` user, check the database status:

```bash
# Connect to SQL*Plus
sqlplus / as sysdba

# Check database status
SELECT status FROM v$instance;

# If status is OPEN, database is running
# If you get "ORA-01034: ORACLE not available", database is not running
```

---

## Step 4: Start Database (if not running)

If the database is not running, start it:

```bash
# As oracle user
sqlplus / as sysdba

# Start the database
STARTUP;

# Verify status
SELECT status FROM v$instance;

# Exit SQL*Plus
EXIT;
```

---

## Step 5: Start Listener (if not running)

```bash
# As oracle user
lsnrctl status

# If listener is not running, start it
lsnrctl start

# Verify listener status
lsnrctl status
```

---

## Step 6: Create systemd Service (Optional)

Since the Alien conversion didn't create the systemd service, you can create it manually:

```bash
# Exit from oracle user back to root
exit

# Create systemd service file
sudo tee /etc/systemd/system/oracle-free-23ai.service > /dev/null << 'EOF'
[Unit]
Description=Oracle Database 23ai Free
After=network.target

[Service]
Type=forking
User=oracle
Group=oinstall
Environment="ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree"
Environment="ORACLE_SID=FREE"
Environment="PATH=/opt/oracle/product/23ai/dbhomeFree/bin:/usr/local/bin:/usr/bin:/bin"
ExecStart=/opt/oracle/product/23ai/dbhomeFree/bin/dbstart /opt/oracle/product/23ai/dbhomeFree
ExecStop=/opt/oracle/product/23ai/dbhomeFree/bin/dbshut /opt/oracle/product/23ai/dbhomeFree
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd
sudo systemctl daemon-reload

# Enable service
sudo systemctl enable oracle-free-23ai

# Start service
sudo systemctl start oracle-free-23ai

# Check status
sudo systemctl status oracle-free-23ai
```

---

## Step 7: Set Environment Variables for Your User

Add Oracle environment variables to your user profile:

```bash
# As your regular user (not oracle, not root)
cat >> ~/.bashrc << 'EOF'

# Oracle Database 23ai Free Environment
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export ORACLE_SID=FREE
export ORACLE_BASE=/opt/oracle
export LD_LIBRARY_PATH=$ORACLE_HOME/lib:$LD_LIBRARY_PATH
export PATH=$ORACLE_HOME/bin:$PATH
export NLS_LANG=AMERICAN_AMERICA.AL32UTF8
EOF

# Reload profile
source ~/.bashrc
```

---

## Step 8: Test Connection

Test the connection from your regular user:

```bash
# Test connection as SYSDBA
sqlplus sys/YourSysPassword@localhost:1521/FREE as sysdba

# If successful, you should see:
# Connected to:
# Oracle Database 23ai Free Release 23.0.0.0.0
```

---

## Step 9: Create ThinkOnErp User

If the database is running and you can connect, create the application user:

```bash
# Connect as SYSDBA
sqlplus sys/YourSysPassword@localhost:1521/FREE as sysdba
```

```sql
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

## Step 10: Test Application User Connection

```bash
# Test connection as application user
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE

# If successful, you're ready to run database scripts
```

---

## Troubleshooting

### Issue: "ORA-01034: ORACLE not available"

**Solution**: Database is not running. Start it:

```bash
sudo su - oracle
sqlplus / as sysdba
STARTUP;
EXIT;
exit
```

### Issue: "ORA-12541: TNS:no listener"

**Solution**: Listener is not running. Start it:

```bash
sudo su - oracle
lsnrctl start
exit
```

### Issue: "ORA-12514: TNS:listener does not currently know of service"

**Solution**: Wait 1-2 minutes after starting the database, or restart the listener:

```bash
sudo su - oracle
lsnrctl stop
lsnrctl start
exit
```

### Issue: Can't find sqlplus command

**Solution**: Set ORACLE_HOME and PATH:

```bash
export ORACLE_HOME=/opt/oracle/product/23ai/dbhomeFree
export PATH=$ORACLE_HOME/bin:$PATH
```

### Issue: "Previous Oracle installation detected"

**Solution**: This is normal. The configuration script detected a previous installation. You can:

1. **Use the existing installation**: Just start the database and verify it works
2. **Reconfigure**: Run the configuration script again with force option
3. **Clean install**: Remove the existing installation and reinstall

To check the existing installation:

```bash
sudo su - oracle
sqlplus / as sysdba
SELECT status FROM v$instance;
EXIT;
exit
```

---

## Quick Start Commands

```bash
# 1. Check if database is running
ps aux | grep pmon

# 2. If not running, start it
sudo su - oracle
sqlplus / as sysdba
STARTUP;
EXIT;
lsnrctl start
exit

# 3. Test connection
sqlplus sys/YourPassword@localhost:1521/FREE as sysdba

# 4. Create application user (if not exists)
# Run the SQL commands from Step 9 above

# 5. Test application user
sqlplus thinkonerp/ThinkOnErp2024@localhost:1521/FREE
```

---

## Next Steps

Once the database is running and you can connect:

1. ✅ Verify database is running
2. ✅ Create ThinkOnErp application user
3. 🔄 Update `.env` file with connection string
4. 🔄 Run ThinkOnErp database scripts (01-89)
5. 🔄 Test API connection
6. 🔄 Deploy ThinkOnErp API

---

## Summary

The Oracle 23ai Free installation is complete, but you need to:

1. **Verify if the database is running** (Step 1-3)
2. **Start the database if needed** (Step 4-5)
3. **Optionally create systemd service** (Step 6)
4. **Create application user** (Step 9)
5. **Test connection** (Step 10)

The "already configured" message means there's a previous Oracle installation. You can either use it or reconfigure it.
