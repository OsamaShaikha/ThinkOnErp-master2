# Docker Connection Troubleshooting Guide

## Problem
Cannot connect to `http://178.104.126.99:5000` - Connection Refused

## Quick Diagnostics

### Step 1: Run Diagnostic Script
```bash
chmod +x diagnose-docker-connection.sh
./diagnose-docker-connection.sh
```

### Step 2: Manual Checks

#### Check Container Status
```bash
docker ps -a | grep thinkonerp-api
```

Expected output: Container should be "Up" status

#### Check Container Logs
```bash
docker logs thinkonerp-api
```

Look for:
- ✅ "Now listening on: http://[::]:8080"
- ✅ "Application started"
- ❌ Any error messages about database connection
- ❌ Any startup failures

#### Check Port Mapping
```bash
docker port thinkonerp-api
```

Expected output: `8080/tcp -> 0.0.0.0:5000`

#### Test from Server
```bash
# Test from localhost
curl http://localhost:5000/health

# Test from server IP
curl http://178.104.126.99:5000/health
```

## Common Issues & Solutions

### Issue 1: Container Not Running
**Symptoms**: `docker ps` shows container is not running or status is "Exited"

**Solution**:
```bash
# Check why it exited
docker logs thinkonerp-api

# Common causes:
# 1. Missing environment variables
# 2. Database connection failure
# 3. Invalid configuration
```

**Fix**:
```bash
# Create .env file with required variables
cat > .env << 'EOF'
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=THINKON_ERP;Password=THINKON_ERP;
JWT_SECRET_KEY=A7fK9xP2LmQ8vR4TzW6nB1CjD5eH3YUs
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60
API_PORT=5000
LOG_LEVEL=Information
EOF

# Restart container
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
```

### Issue 2: Firewall Blocking Port
**Symptoms**: Container is running but connection refused from external IP

**Solution**:
```bash
# Check firewall status
sudo ufw status

# Allow port 5000
sudo ufw allow 5000/tcp

# Or disable firewall temporarily for testing
sudo ufw disable
```

### Issue 3: Port Already in Use
**Symptoms**: Container fails to start, logs show "address already in use"

**Solution**:
```bash
# Find what's using port 5000
sudo netstat -tuln | grep 5000
sudo lsof -i :5000

# Kill the process or change API_PORT in .env
echo "API_PORT=5001" >> .env

# Restart
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
```

### Issue 4: Database Connection Failure
**Symptoms**: Container starts but crashes immediately, logs show Oracle connection errors

**Solution**:
```bash
# Test database connection from server
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1

# If connection fails, check:
# 1. Oracle listener is running
# 2. Firewall allows port 1521
# 3. Database credentials are correct

# Update connection string in .env if needed
```

### Issue 5: Container Running but Not Responding
**Symptoms**: Container is "Up" but health check fails

**Solution**:
```bash
# Check if app is listening inside container
docker exec thinkonerp-api netstat -tuln | grep 8080

# Check application logs for errors
docker logs -f thinkonerp-api

# Restart container
docker restart thinkonerp-api
```

### Issue 6: Binding to Wrong Interface
**Symptoms**: Works on localhost but not on external IP

**Solution**:
```bash
# Verify ASPNETCORE_URLS is set correctly
docker exec thinkonerp-api printenv | grep ASPNETCORE_URLS

# Should be: http://+:8080 or http://0.0.0.0:8080
# NOT: http://localhost:8080 or http://127.0.0.1:8080

# If wrong, update docker-compose.simple.yml and restart
```

## Step-by-Step Resolution

### 1. Check Container Status
```bash
docker ps -a | grep thinkonerp-api
```

If status is "Exited":
```bash
docker logs thinkonerp-api
# Fix the error shown in logs
docker-compose -f docker-compose.simple.yml up -d
```

### 2. Check Logs for Errors
```bash
docker logs --tail 100 thinkonerp-api
```

Look for:
- Database connection errors → Fix connection string
- Missing configuration → Add to .env file
- Port binding errors → Change API_PORT

### 3. Test Internal Connectivity
```bash
# Test from inside container
docker exec thinkonerp-api curl http://localhost:8080/health

# If this works, issue is with port mapping or firewall
```

### 4. Test Port Mapping
```bash
# From server
curl http://localhost:5000/health

# If this works but external IP doesn't, issue is firewall
```

### 5. Check Firewall
```bash
# Check firewall rules
sudo ufw status numbered

# Allow port 5000
sudo ufw allow 5000/tcp

# Test again
curl http://178.104.126.99:5000/health
```

### 6. Check Network Binding
```bash
# Verify port is bound to 0.0.0.0 (all interfaces)
sudo netstat -tuln | grep 5000

# Should show: 0.0.0.0:5000
# NOT: 127.0.0.1:5000
```

## Testing Endpoints

Once connection is working:

### Health Check
```bash
curl http://178.104.126.99:5000/health
```

Expected: `{"status":"Healthy"}`

### Swagger UI
Open in browser:
```
http://178.104.126.99:5000/swagger
```

### Test Login
```bash
curl -X POST http://178.104.126.99:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "superadmin",
    "password": "Admin@123"
  }'
```

## Quick Restart Commands

### Restart Container
```bash
docker restart thinkonerp-api
```

### Rebuild and Restart
```bash
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml build --no-cache
docker-compose -f docker-compose.simple.yml up -d
```

### View Live Logs
```bash
docker logs -f thinkonerp-api
```

### Stop Everything
```bash
docker-compose -f docker-compose.simple.yml down
```

## Environment Variables Checklist

Ensure these are set in `.env` file:

- ✅ `ORACLE_CONNECTION_STRING` - Database connection
- ✅ `JWT_SECRET_KEY` - JWT signing key
- ✅ `JWT_ISSUER` - JWT issuer
- ✅ `JWT_AUDIENCE` - JWT audience
- ✅ `JWT_EXPIRY_MINUTES` - Token expiry (default: 60)
- ✅ `API_PORT` - External port (default: 5000)
- ✅ `LOG_LEVEL` - Logging level (default: Information)

## Need More Help?

Run the diagnostic script and share the output:
```bash
./diagnose-docker-connection.sh > diagnostics.txt
cat diagnostics.txt
```
