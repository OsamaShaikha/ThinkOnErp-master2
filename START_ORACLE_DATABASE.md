# Start Oracle Database in Docker

## Problem
Your ThinkOnErp API cannot connect to the database because Oracle is not running.

## Solution: Start Oracle Database Container

### Step 1: Pull Oracle XE Image
```bash
docker pull container-registry.oracle.com/database/express:21.3.0-xe
```

### Step 2: Start Oracle Database Container
```bash
docker run -d \
  --name oracle-xe \
  -p 1521:1521 \
  -p 5500:5500 \
  -e ORACLE_PWD=YourStrongPassword123 \
  -e ORACLE_CHARACTERSET=AL32UTF8 \
  -v oracle-data:/opt/oracle/oradata \
  container-registry.oracle.com/database/express:21.3.0-xe
```

**Parameters Explained**:
- `--name oracle-xe`: Container name
- `-p 1521:1521`: Oracle listener port
- `-p 5500:5500`: Oracle Enterprise Manager port
- `-e ORACLE_PWD`: SYS/SYSTEM password
- `-e ORACLE_CHARACTERSET`: Character set (supports Arabic)
- `-v oracle-data:/opt/oracle/oradata`: Persistent storage

### Step 3: Wait for Database to Start (takes 2-5 minutes)
```bash
# Watch the logs
docker logs -f oracle-xe

# Wait for this message:
# DATABASE IS READY TO USE!
```

### Step 4: Connect and Create User
```bash
# Connect as SYSDBA
docker exec -it oracle-xe sqlplus sys/YourStrongPassword123@XEPDB1 as sysdba

# Create your application user
CREATE USER thinkonerp IDENTIFIED BY YourAppPassword123;
GRANT CONNECT, RESOURCE, DBA TO thinkonerp;
GRANT UNLIMITED TABLESPACE TO thinkonerp;
EXIT;
```

### Step 5: Update Connection String
Update your API container to use the correct connection string:

```bash
# Stop the current API container
docker stop thinkonerp-api
docker rm thinkonerp-api

# Start with correct connection string
docker run -d \
  --name thinkonerp-api \
  --link oracle-xe:oracle \
  -p 5000:5000 \
  -e ConnectionStrings__OracleDb="Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle-xe)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=thinkonerp;Password=YourAppPassword123;" \
  devosamaarori/thinkonerp-api:v1
```

### Step 6: Run Database Scripts
```bash
# Copy scripts to container
docker cp Database/Scripts oracle-xe:/tmp/

# Execute scripts
docker exec -it oracle-xe sqlplus thinkonerp/YourAppPassword123@XEPDB1 @/tmp/Scripts/01_Create_Sequences.sql
# ... run all scripts in order
```

---

## Option 2: Use Docker Compose (Easier)

Create `docker-compose-with-db.yml`:

```yaml
version: '3.8'

services:
  oracle-db:
    image: container-registry.oracle.com/database/express:21.3.0-xe
    container_name: oracle-xe
    ports:
      - "1521:1521"
      - "5500:5500"
    environment:
      - ORACLE_PWD=YourStrongPassword123
      - ORACLE_CHARACTERSET=AL32UTF8
    volumes:
      - oracle-data:/opt/oracle/oradata
      - ./Database/Scripts:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD", "sqlplus", "-L", "sys/YourStrongPassword123@XEPDB1 as sysdba", "@/dev/null"]
      interval: 30s
      timeout: 10s
      retries: 5

  thinkonerp-api:
    image: devosamaarori/thinkonerp-api:v1
    container_name: thinkonerp-api
    ports:
      - "5000:5000"
    environment:
      - ConnectionStrings__OracleDb=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle-db)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=thinkonerp;Password=YourAppPassword123;
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      oracle-db:
        condition: service_healthy
    restart: unless-stopped

volumes:
  oracle-data:
```

**Start everything**:
```bash
docker-compose -f docker-compose-with-db.yml up -d
```

---

## Option 3: Use External Oracle Database

If you have Oracle installed on the host machine (not in Docker):

### Check if Oracle is Running
```bash
# On Linux
sudo systemctl status oracle-xe

# Or check listener
lsnrctl status
```

### Start Oracle if Stopped
```bash
# On Linux
sudo systemctl start oracle-xe
lsnrctl start

# On Windows
# Start Oracle services from Services panel
```

### Update API Connection String
```bash
docker run -d \
  --name thinkonerp-api \
  --add-host=host.docker.internal:host-gateway \
  -p 5000:5000 \
  -e ConnectionStrings__OracleDb="Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=host.docker.internal)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=thinkonerp;Password=YourPassword;" \
  devosamaarori/thinkonerp-api:v1
```

---

## Quick Diagnostic Commands

```bash
# Check what containers are running
docker ps -a

# Check if port 1521 is listening
netstat -an | grep 1521

# Test Oracle connectivity from host
sqlplus thinkonerp/password@localhost:1521/XEPDB1

# Test from inside API container
docker exec -it thinkonerp-api bash
# Then try: telnet oracle-xe 1521
```

---

## Recommended Approach

**For Development**: Use Option 2 (Docker Compose) - easiest to manage

**For Production**: Use Option 3 (External Oracle) - better performance and data persistence

---

## Next Steps After Database is Running

1. ✅ Start Oracle database
2. ✅ Create application user
3. ✅ Run all database scripts (01-89)
4. ✅ Start API container with correct connection string
5. ✅ Test API endpoints
6. ✅ Verify data is persisted

---

## Common Issues

### Issue: "ORA-12541: TNS:no listener"
**Solution**: Oracle listener is not running. Start it with `lsnrctl start`

### Issue: "ORA-01017: invalid username/password"
**Solution**: Check credentials in connection string

### Issue: "ORA-12514: TNS:listener does not currently know of service"
**Solution**: Wait for database to fully start (check logs)

### Issue: Container cannot reach host Oracle
**Solution**: Use `--add-host=host.docker.internal:host-gateway` flag
