# 🚀 Complete Docker Hub Deployment Guide
## Deploy ThinkOnErp API + Oracle Database to Cloud Server

---

## 📋 What You'll Deploy

1. **Custom Oracle Database Image** - Pre-loaded with ThinkOnErp schema and test data
2. **ThinkOnErp API Image** - Your .NET 8 Web API application
3. **Docker Compose** - Orchestrates both services with networking

---

## 🎯 Prerequisites

### On Your Local Windows Machine:
- ✅ Docker Desktop installed and running
- ✅ Docker Hub account (free): https://hub.docker.com
- ✅ Git Bash or PowerShell

### On Your Cloud Server (178.104.126.99):
- ✅ Docker installed
- ✅ Docker Compose installed
- ✅ SSH access (root/ThinkOnErp!@123)

---

## 📦 PART 1: Build and Push Images (Local Machine)

### Step 1.1: Login to Docker Hub

Open PowerShell in your project directory:

```powershell
cd D:\ThinkOnErp
docker login
```

Enter your Docker Hub username and password when prompted.

### Step 1.2: Build Oracle Database Image

```powershell
docker build -t yourusername/thinkonerp-oracle:v1.0 -f Dockerfile.oracle .
```

**⏱️ This will take 5-10 minutes** (Oracle base image is large ~2GB)

**Replace `yourusername` with your actual Docker Hub username!**

### Step 1.3: Build API Image

```powershell
docker build -t yourusername/thinkonerp-api:v1.0 .
```

**⏱️ This will take 2-5 minutes**

### Step 1.4: Push Both Images to Docker Hub

```powershell
# Push Oracle image
docker push yourusername/thinkonerp-oracle:v1.0

# Push API image
docker push yourusername/thinkonerp-api:v1.0
```

**⏱️ This will take 10-20 minutes depending on your internet speed**

You can verify the upload at: `https://hub.docker.com/r/yourusername/`

---

## 🌐 PART 2: Deploy on Cloud Server

### Step 2.1: Connect to Server

```bash
ssh root@178.104.126.99
```

Password: `ThinkOnErp!@123`

### Step 2.2: Create Project Directory

```bash
mkdir -p ~/ThinkOnErp
cd ~/ThinkOnErp
```

### Step 2.3: Create .env File

```bash
cat > .env << 'EOF'
# JWT Settings
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGenerationAndValidation123!
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60

# Audit Trail Settings
AUDIT__PAYLOADLOGGINGLEVEL=MetadataOnly
AUDIT__ENCRYPTIONKEY=dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz
AUDIT__SIGNINGKEY=dGhpc2lzYXRlc3RzaWduaW5na2V5Zm9yYXVkaXRsb2dz

# Alert Settings
ALERT__WEBHOOKURL=https://example.com/webhook
ALERT__NOTIFICATIONTIMEOUTSECONDS=30
ALERT__MAXRETRYATTEMPTS=2
ALERT__RETRYDELAYMS=5000

# Redis (optional - leave empty if not using)
REDIS__CONNECTIONSTRING=

# Logging
LOG_LEVEL=Information

# API Port
API_PORT=5000

# OpenTelemetry (optional - leave empty if not using)
OPENTELEMETRY__OTLPENDPOINT=
EOF
```

### Step 2.4: Create docker-compose.yml

```bash
cat > docker-compose.yml << 'EOF'
version: '3.8'

services:
  # Oracle Database with ThinkOnErp Schema Pre-loaded
  oracle-db:
    image: yourusername/thinkonerp-oracle:v1.0
    container_name: thinkonerp-oracle
    environment:
      - ORACLE_PWD=OraclePassword123
      - ORACLE_CHARACTERSET=AL32UTF8
    ports:
      - "1521:1521"
      - "5500:5500"
    volumes:
      - oracle-data:/opt/oracle/oradata
    networks:
      - thinkonerp-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "echo 'SELECT 1 FROM DUAL;' | sqlplus -s THINKON_ERP/THINKON_ERP@//localhost:1521/XEPDB1 || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 120s

  # ThinkOnErp API
  thinkonerp-api:
    image: yourusername/thinkonerp-api:v1.0
    container_name: thinkonerp-api
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__OracleDb=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=oracle-db:1521/XEPDB1
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - JwtSettings__Issuer=${JWT_ISSUER}
      - JwtSettings__Audience=${JWT_AUDIENCE}
      - JwtSettings__ExpiryInMinutes=${JWT_EXPIRY_MINUTES}
      - AuditTrail__PayloadLoggingLevel=${AUDIT__PAYLOADLOGGINGLEVEL}
      - AuditTrail__EncryptionKey=${AUDIT__ENCRYPTIONKEY}
      - AuditTrail__SigningKey=${AUDIT__SIGNINGKEY}
      - AlertSettings__WebhookUrl=${ALERT__WEBHOOKURL}
      - AlertSettings__NotificationTimeoutSeconds=${ALERT__NOTIFICATIONTIMEOUTSECONDS}
      - AlertSettings__MaxRetryAttempts=${ALERT__MAXRETRYATTEMPTS}
      - AlertSettings__RetryDelayMs=${ALERT__RETRYDELAYMS}
      - RedisSettings__ConnectionString=${REDIS__CONNECTIONSTRING}
      - Serilog__MinimumLevel__Default=${LOG_LEVEL}
      - OpenTelemetry__OtlpEndpoint=${OPENTELEMETRY__OTLPENDPOINT}
    ports:
      - "${API_PORT:-5000}:8080"
    depends_on:
      oracle-db:
        condition: service_healthy
    networks:
      - thinkonerp-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 3s
      retries: 3
      start_period: 30s
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    volumes:
      - ./logs:/app/logs

volumes:
  oracle-data:
    driver: local

networks:
  thinkonerp-network:
    driver: bridge
EOF
```

**⚠️ IMPORTANT:** Replace `yourusername` with your Docker Hub username in the file above!

You can do this with:

```bash
# Replace 'yourusername' with your actual Docker Hub username
sed -i 's/yourusername/YOUR_ACTUAL_USERNAME/g' docker-compose.yml
```

### Step 2.5: Pull Images from Docker Hub

```bash
docker-compose pull
```

**⏱️ This will take 5-10 minutes** (downloading both images)

### Step 2.6: Start the Services

```bash
docker-compose up -d
```

**⏱️ Oracle will take 2-3 minutes to initialize on first start**

### Step 2.7: Monitor Startup

Watch the logs to see when everything is ready:

```bash
# Watch Oracle initialization
docker logs -f thinkonerp-oracle

# Press Ctrl+C when you see "DATABASE IS READY TO USE!"

# Watch API startup
docker logs -f thinkonerp-api

# Press Ctrl+C when you see "Application started"
```

---

## ✅ PART 3: Verify Deployment

### Check Container Status

```bash
docker ps
```

You should see both containers running:
- `thinkonerp-oracle` (healthy)
- `thinkonerp-api` (healthy)

### Test Database Connection

```bash
docker exec -it thinkonerp-oracle sqlplus THINKON_ERP/THINKON_ERP@//localhost:1521/XEPDB1
```

Type `EXIT` to quit.

### Test API Health Endpoint

```bash
curl http://localhost:8080/health
```

Should return: `Healthy`

### Test API from Outside Server

From your local machine or browser:

**Swagger UI:** http://178.104.126.99:5000/swagger

### Test Login API

```bash
curl -X POST http://178.104.126.99:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"superadmin","password":"Admin@123"}'
```

Should return a JWT token!

---

## 🔄 PART 4: Update Deployment (When You Make Changes)

### On Your Local Machine:

```powershell
cd D:\ThinkOnErp

# Build new version
docker build -t yourusername/thinkonerp-api:v1.1 .

# Push to Docker Hub
docker push yourusername/thinkonerp-api:v1.1
```

### On Your Server:

```bash
cd ~/ThinkOnErp

# Update docker-compose.yml to use v1.1
sed -i 's/:v1.0/:v1.1/g' docker-compose.yml

# Pull new image
docker-compose pull

# Restart with new image
docker-compose up -d
```

---

## 🛠️ PART 5: Management Commands

### View Logs

```bash
# All services
docker-compose logs -f

# Just API
docker logs -f thinkonerp-api

# Just Oracle
docker logs -f thinkonerp-oracle
```

### Restart Services

```bash
# Restart everything
docker-compose restart

# Restart just API
docker-compose restart thinkonerp-api
```

### Stop Services

```bash
docker-compose stop
```

### Start Services

```bash
docker-compose start
```

### Stop and Remove Everything

```bash
docker-compose down
```

### Stop and Remove Everything Including Data

```bash
docker-compose down -v
```

**⚠️ WARNING:** This will delete all database data!

---

## 🔍 PART 6: Troubleshooting

### API Container Keeps Restarting

```bash
# Check logs
docker logs thinkonerp-api

# Common issues:
# 1. Database not ready yet - wait 2-3 minutes
# 2. Configuration error - check .env file
# 3. Connection string wrong - verify oracle-db hostname
```

### Oracle Container Won't Start

```bash
# Check logs
docker logs thinkonerp-oracle

# Common issues:
# 1. Not enough memory - Oracle needs at least 2GB RAM
# 2. Port 1521 already in use - stop other Oracle instances
# 3. Disk space - Oracle needs at least 10GB free space
```

### Can't Access Swagger UI

```bash
# Check if API is running
docker ps | grep thinkonerp-api

# Check if port is open
netstat -tulpn | grep 5000

# Check firewall
ufw status
ufw allow 5000/tcp
```

### Database Connection Errors

```bash
# Test from API container
docker exec -it thinkonerp-api sh
# Inside container:
ping oracle-db
exit

# Test from host
docker exec -it thinkonerp-oracle sqlplus THINKON_ERP/THINKON_ERP@//localhost:1521/XEPDB1
```

---

## 📊 PART 7: What's Included in Oracle Image

The custom Oracle image includes:

✅ **All Database Tables:**
- SYS_ROLE, SYS_CURRENCY, SYS_BRANCH, SYS_COMPANY, SYS_USERS
- SYS_SUPER_ADMIN, SYS_FISCAL_YEAR
- Permissions tables (SYS_SYSTEM, SYS_SCREEN, SYS_PERMISSION, etc.)
- Audit trail tables (SYS_AUDIT_LOG, SYS_AUDIT_LOG_ARCHIVE, etc.)
- Ticket system tables
- Search and analytics tables

✅ **All Stored Procedures:**
- CRUD operations for all entities
- Authentication procedures
- Audit trail procedures
- Ticket management procedures
- Search procedures

✅ **Test Data:**
- superadmin / Admin@123
- moe / Admin@123
- user1 / User@123
- Sample companies, branches, roles, currencies

✅ **Sequences and Indexes:**
- All sequences for ID generation
- Performance indexes
- Full-text search indexes

---

## 🎉 Success Criteria

Your deployment is successful when:

1. ✅ Both containers show "healthy" status in `docker ps`
2. ✅ Swagger UI loads at http://178.104.126.99:5000/swagger
3. ✅ Login API returns JWT token for superadmin/Admin@123
4. ✅ You can create/read/update entities via API
5. ✅ Audit logs are being created in database

---

## 📝 Quick Reference

| Item | Value |
|------|-------|
| **Docker Hub Oracle Image** | `yourusername/thinkonerp-oracle:v1.0` |
| **Docker Hub API Image** | `yourusername/thinkonerp-api:v1.0` |
| **Server IP** | 178.104.126.99 |
| **API Port** | 5000 |
| **Oracle Port** | 1521 |
| **Swagger URL** | http://178.104.126.99:5000/swagger |
| **Test User** | superadmin / Admin@123 |
| **Database User** | THINKON_ERP / THINKON_ERP |
| **Database Service** | XEPDB1 |
| **Server Path** | ~/ThinkOnErp |

---

## 🔐 Security Notes

1. **Change default passwords** in production:
   - Oracle SYSTEM password
   - THINKON_ERP database password
   - JWT secret key
   - Audit encryption/signing keys

2. **Use HTTPS** in production (add nginx reverse proxy)

3. **Restrict ports** with firewall:
   ```bash
   ufw allow 5000/tcp  # API
   ufw deny 1521/tcp   # Oracle (only allow from API container)
   ```

4. **Use Docker secrets** for sensitive data instead of .env file

---

## 🎯 Next Steps

1. ✅ Set up automated backups for Oracle data volume
2. ✅ Configure nginx reverse proxy with SSL
3. ✅ Set up monitoring (Prometheus + Grafana)
4. ✅ Configure log aggregation (ELK stack)
5. ✅ Set up CI/CD pipeline for automated deployments

---

## 💡 Tips

- **Use version tags** (v1.0, v1.1) instead of `:latest` for better control
- **Tag stable releases** with `:stable` tag for easy rollback
- **Keep .env file secure** - never commit to git
- **Monitor disk space** - Oracle logs can grow large
- **Regular backups** - backup oracle-data volume daily

---

## 📞 Support

If you encounter issues:

1. Check logs: `docker-compose logs -f`
2. Verify configuration: `cat .env`
3. Check container health: `docker ps`
4. Test connectivity: `docker exec -it thinkonerp-api ping oracle-db`

---

**🎉 Congratulations! Your ThinkOnErp system is now running on Docker Hub images!**
