# 🐳 Docker Hub Deployment Guide

## Overview
This guide shows how to build your ThinkOnErp API image, push it to Docker Hub, and deploy it on your cloud server.

---

## 📋 Prerequisites

1. **Docker Hub Account**: Create one at https://hub.docker.com (free)
2. **Docker installed** on your local Windows machine
3. **Your cloud server** with Docker installed (178.104.126.99)

---

## 🔨 Step 1: Build and Push Image (On Your Local Machine)

### 1.1 Login to Docker Hub

Open PowerShell and run:

```powershell
docker login
```

Enter your Docker Hub username and password.

### 1.2 Build the Image

```powershell
cd D:\ThinkOnErp
docker build -t yourusername/thinkonerp-api:latest .
```

**Replace `yourusername` with your actual Docker Hub username!**

### 1.3 Push to Docker Hub

```powershell
docker push yourusername/thinkonerp-api:latest
```

This will upload your image to Docker Hub (may take 5-10 minutes depending on your internet speed).

---

## 🌐 Step 2: Deploy on Cloud Server

### 2.1 Create docker-compose.yml on Server

SSH to your server:

```bash
ssh root@178.104.126.99
```

Password: `ThinkOnErp!@123`

### 2.2 Create Project Directory

```bash
mkdir -p ~/ThinkOnErp
cd ~/ThinkOnErp
```

### 2.3 Create .env File

```bash
cat > .env << 'EOF'
ORACLE_CONNECTION_STRING=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=178.104.126.99:1521/XEPDB1
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGenerationAndValidation123!
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60
AUDIT__PAYLOADLOGGINGLEVEL=MetadataOnly
AUDIT__ENCRYPTIONKEY=dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz
AUDIT__SIGNINGKEY=dGhpc2lzYXRlc3RzaWduaW5na2V5Zm9yYXVkaXRsb2dz
ALERT__WEBHOOKURL=https://example.com/webhook
ALERT__NOTIFICATIONTIMEOUTSECONDS=30
ALERT__MAXRETRYATTEMPTS=2
ALERT__RETRYDELAYMS=5000
REDIS__CONNECTIONSTRING=
LOG_LEVEL=Information
API_PORT=5000
OPENTELEMETRY__OTLPENDPOINT=
EOF
```

### 2.4 Create docker-compose.yml

```bash
cat > docker-compose.yml << 'EOF'
version: '3.8'

services:
  thinkonerp-api:
    image: yourusername/thinkonerp-api:latest
    container_name: thinkonerp-api
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__OracleDb=${ORACLE_CONNECTION_STRING}
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
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 3s
      retries: 3
      start_period: 10s
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    volumes:
      - ./logs:/app/logs
EOF
```

**⚠️ IMPORTANT: Replace `yourusername` with your Docker Hub username in the file above!**

### 2.5 Start the Application

```bash
docker-compose pull
docker-compose up -d
```

### 2.6 Check Logs

```bash
docker logs -f thinkonerp-api
```

Press `Ctrl+C` to stop watching logs.

---

## ✅ Step 3: Verify Deployment

### Test Health Endpoint

```bash
curl http://localhost:8080/health
```

### Access Swagger UI

Open your browser and go to:

**http://178.104.126.99:5000/swagger**

### Test Login API

```bash
curl -X POST http://178.104.126.99:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"superadmin","password":"Admin@123"}'
```

---

## 🔄 Step 4: Update Deployment (When You Make Changes)

### On Your Local Machine:

```powershell
cd D:\ThinkOnErp
docker build -t yourusername/thinkonerp-api:latest .
docker push yourusername/thinkonerp-api:latest
```

### On Your Server:

```bash
cd ~/ThinkOnErp
docker-compose pull
docker-compose up -d
```

---

## 🗄️ Optional: Add Oracle Database to Docker Compose

If you want to run Oracle Database in Docker too:

```yaml
version: '3.8'

services:
  oracle-db:
    image: container-registry.oracle.com/database/express:21c-slim
    container_name: oracle-xe
    environment:
      - ORACLE_PWD=OraclePassword123
      - ORACLE_CHARACTERSET=AL32UTF8
    ports:
      - "1521:1521"
      - "5500:5500"
    volumes:
      - oracle-data:/opt/oracle/oradata
    restart: unless-stopped

  thinkonerp-api:
    image: yourusername/thinkonerp-api:latest
    container_name: thinkonerp-api
    depends_on:
      - oracle-db
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__OracleDb=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=oracle-db:1521/XEPDB1
    ports:
      - "5000:8080"
    restart: unless-stopped
    volumes:
      - ./logs:/app/logs

volumes:
  oracle-data:
```

---

## 🔍 Troubleshooting

### Check Container Status

```bash
docker ps
```

### View Logs

```bash
docker logs thinkonerp-api
```

### Restart Container

```bash
docker-compose restart
```

### Stop Everything

```bash
docker-compose down
```

### Remove Everything and Start Fresh

```bash
docker-compose down -v
docker-compose up -d
```

---

## 📝 Quick Reference

| Item | Value |
|------|-------|
| **Docker Hub Image** | `yourusername/thinkonerp-api:latest` |
| **Server IP** | 178.104.126.99 |
| **API Port** | 5000 |
| **Container Port** | 8080 |
| **Swagger URL** | http://178.104.126.99:5000/swagger |
| **Test User** | superadmin / Admin@123 |
| **Server Path** | ~/ThinkOnErp |

---

## 🎉 Benefits of This Approach

1. ✅ **No source code on server** - only docker-compose.yml and .env
2. ✅ **Easy updates** - just push to Docker Hub and pull on server
3. ✅ **Consistent environment** - same image everywhere
4. ✅ **Fast deployment** - no building on server
5. ✅ **Easy rollback** - use image tags (`:v1.0`, `:v1.1`, etc.)

---

## 🏷️ Using Version Tags (Recommended)

Instead of `:latest`, use version tags:

```powershell
# On local machine
docker build -t yourusername/thinkonerp-api:v1.0 .
docker push yourusername/thinkonerp-api:v1.0
```

```yaml
# In docker-compose.yml on server
services:
  thinkonerp-api:
    image: yourusername/thinkonerp-api:v1.0
```

This makes it easy to rollback to previous versions if needed!
