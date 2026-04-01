# ThinkOnErp API - Simple Docker Deployment (API Only)

This guide is for deploying only the ThinkOnErp API when you already have an Oracle database deployed.

## Prerequisites

- Ubuntu 20.04 or later
- Docker and Docker Compose installed
- Existing Oracle database (already deployed)
- Oracle database connection details
- Minimum 2GB RAM for the API container

## Quick Start (3 Steps)

### Step 1: Configure Environment

```bash
# Copy environment template
cp .env.example .env

# Edit configuration
nano .env
```

Update these required values in `.env`:

```bash
# Your Oracle database connection
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.1.100)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCL)));User Id=SYSTEM;Password=YourPassword;

# Generate a secure JWT secret (at least 32 characters)
JWT_SECRET_KEY=your-secure-random-key-at-least-32-characters-long
```

### Step 2: Deploy

```bash
# Make script executable
chmod +x deploy-simple.sh

# Run deployment
./deploy-simple.sh
```

The script will:
- ✓ Check Docker installation
- ✓ Validate configuration
- ✓ Build the API image
- ✓ Start the container
- ✓ Test health endpoint

### Step 3: Test

```bash
# Test health endpoint
curl http://localhost:5000/health

# Test login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"Admin@123"}'
```

## Manual Deployment

If you prefer manual deployment:

```bash
# 1. Configure environment
cp .env.example .env
nano .env

# 2. Build image
docker-compose -f docker-compose.simple.yml build

# 3. Start service
docker-compose -f docker-compose.simple.yml up -d

# 4. Check status
docker-compose -f docker-compose.simple.yml ps

# 5. View logs
docker-compose -f docker-compose.simple.yml logs -f
```

## Configuration Details

### Oracle Connection String Format

```bash
# Standard format
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=hostname)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=service_name)));User Id=username;Password=password;

# With SID instead of SERVICE_NAME
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=hostname)(PORT=1521))(CONNECT_DATA=(SID=ORCL)));User Id=username;Password=password;

# Oracle Cloud with SSL
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)(HOST=hostname)(PORT=1522))(CONNECT_DATA=(SERVICE_NAME=service_name)));User Id=username;Password=password;
```

### JWT Secret Key

Generate a secure random key:

```bash
# Using OpenSSL
openssl rand -base64 32

# Using /dev/urandom
cat /dev/urandom | tr -dc 'a-zA-Z0-9' | fold -w 32 | head -n 1
```

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ORACLE_CONNECTION_STRING` | Yes | - | Oracle database connection |
| `JWT_SECRET_KEY` | Yes | - | JWT signing key (32+ chars) |
| `JWT_ISSUER` | No | ThinkOnErpAPI | JWT token issuer |
| `JWT_AUDIENCE` | No | ThinkOnErpClient | JWT token audience |
| `JWT_EXPIRY_MINUTES` | No | 60 | Token expiration time |
| `LOG_LEVEL` | No | Information | Logging level |
| `API_PORT` | No | 5000 | External API port |

## Service Management

### View Logs

```bash
# Follow logs
docker-compose -f docker-compose.simple.yml logs -f

# Last 100 lines
docker-compose -f docker-compose.simple.yml logs --tail=100

# Specific time range
docker-compose -f docker-compose.simple.yml logs --since 30m
```

### Restart Service

```bash
docker-compose -f docker-compose.simple.yml restart
```

### Stop Service

```bash
docker-compose -f docker-compose.simple.yml down
```

### Update and Redeploy

```bash
# Pull latest code
git pull

# Rebuild and restart
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml build --no-cache
docker-compose -f docker-compose.simple.yml up -d
```

### Check Container Status

```bash
# Container status
docker-compose -f docker-compose.simple.yml ps

# Container health
docker inspect thinkonerp-api | grep -A 10 Health

# Resource usage
docker stats thinkonerp-api
```

## API Endpoints

Once deployed, the API is available at:

- **Base URL**: `http://localhost:5000`
- **Swagger UI**: `http://localhost:5000/swagger`
- **Health Check**: `http://localhost:5000/health`

### Authentication

```bash
# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "admin",
    "password": "Admin@123"
  }'

# Response
{
  "success": true,
  "statusCode": 200,
  "message": "Login successful",
  "data": {
    "accessToken": "eyJhbGc...",
    "expiresAt": "2024-01-01T12:00:00Z",
    "tokenType": "Bearer"
  }
}
```

### Using the Token

```bash
# Get all roles
TOKEN="your-jwt-token-here"
curl http://localhost:5000/api/roles \
  -H "Authorization: Bearer $TOKEN"
```

## Troubleshooting

### API Not Starting

1. **Check logs**:
```bash
docker-compose -f docker-compose.simple.yml logs thinkonerp-api
```

2. **Common issues**:
   - Oracle connection string incorrect
   - Oracle database not accessible from container
   - JWT secret key too short
   - Port 5000 already in use

### Oracle Connection Issues

1. **Test Oracle connectivity from container**:
```bash
docker exec -it thinkonerp-api ping your-oracle-host
```

2. **Check Oracle is listening**:
```bash
telnet your-oracle-host 1521
```

3. **Verify Oracle credentials**:
   - Username and password correct
   - User has necessary permissions
   - Service name or SID is correct

### Port Already in Use

```bash
# Find process using port 5000
sudo lsof -i :5000

# Kill process
sudo kill -9 <PID>

# Or change port in .env
echo "API_PORT=5001" >> .env
```

### Container Keeps Restarting

```bash
# Check logs for errors
docker logs thinkonerp-api

# Check container exit code
docker inspect thinkonerp-api | grep -A 5 State
```

### Oracle Connection Timeout

If Oracle is on a different network:

1. **Check firewall rules**:
```bash
sudo ufw status
sudo ufw allow from <api-container-ip> to any port 1521
```

2. **Check Oracle listener**:
```bash
# On Oracle server
lsnrctl status
```

3. **Use host network mode** (if needed):

Edit `docker-compose.simple.yml`:
```yaml
services:
  thinkonerp-api:
    network_mode: "host"
    # Remove ports section when using host mode
```

## Production Recommendations

### 1. Use HTTPS

Add Nginx reverse proxy with SSL:

```bash
# Install Certbot
sudo apt-get install certbot

# Generate certificate
sudo certbot certonly --standalone -d your-domain.com

# Use nginx configuration from DEPLOYMENT.md
```

### 2. Secure Environment Variables

```bash
# Use Docker secrets instead of .env
echo "your-secret-key" | docker secret create jwt_secret -

# Update docker-compose.simple.yml to use secrets
```

### 3. Set Resource Limits

Edit `docker-compose.simple.yml`:

```yaml
services:
  thinkonerp-api:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '1'
          memory: 1G
```

### 4. Enable Log Rotation

Already configured in `docker-compose.simple.yml`:
```yaml
logging:
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
```

### 5. Monitor the Service

```bash
# Install monitoring tools
docker run -d --name=prometheus prom/prometheus
docker run -d --name=grafana grafana/grafana
```

### 6. Backup Configuration

```bash
# Backup .env file
cp .env .env.backup

# Store securely (not in git)
chmod 600 .env.backup
```

### 7. Regular Updates

```bash
# Create update script
cat > update.sh << 'EOF'
#!/bin/bash
git pull
docker-compose -f docker-compose.simple.yml build --no-cache
docker-compose -f docker-compose.simple.yml up -d
EOF

chmod +x update.sh
```

## Uninstalling

```bash
# Stop and remove container
docker-compose -f docker-compose.simple.yml down

# Remove image
docker rmi $(docker images 'thinkonerp*' -q)

# Remove volumes (if any)
docker volume prune

# Remove configuration
rm .env
```

## Support

For issues:
1. Check logs: `docker-compose -f docker-compose.simple.yml logs -f`
2. Verify Oracle connection
3. Check environment variables
4. Review DEPLOYMENT.md for detailed troubleshooting

## Quick Reference

```bash
# Deploy
./deploy-simple.sh

# View logs
docker-compose -f docker-compose.simple.yml logs -f

# Restart
docker-compose -f docker-compose.simple.yml restart

# Stop
docker-compose -f docker-compose.simple.yml down

# Update
docker-compose -f docker-compose.simple.yml up -d --build

# Health check
curl http://localhost:5000/health

# Test login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"Admin@123"}'
```
