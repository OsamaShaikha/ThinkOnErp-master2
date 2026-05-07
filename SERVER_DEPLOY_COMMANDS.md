# Server Deployment Commands

## Quick Deploy (Copy-Paste on Server)

After you've pushed your image to Docker Hub, SSH to your server and run these commands.

**⚠️ IMPORTANT:** Replace `YOUR_DOCKERHUB_USERNAME` with your actual Docker Hub username!

---

## Option 1: Complete One-Command Deploy

Copy and paste this **entire block** into your SSH terminal:

```bash
DOCKER_USER="YOUR_DOCKERHUB_USERNAME" && \
cd ~/ThinkOnErp && \
docker-compose down 2>/dev/null || true && \
docker stop thinkonerp-api 2>/dev/null || true && \
docker rm thinkonerp-api 2>/dev/null || true && \
cat > .env << 'EOF'
ORACLE_CONNECTION_STRING=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=178.104.126.99:1521/XEPDB1
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGenerationAndValidation123!
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60
AUDIT__PAYLOADLOGGINGLEVEL=MetadataOnly
AUDIT__ENCRYPTIONKEY=dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz
AUDIT__SIGNINGKEY=dGhpc2lzYXRlc3RzaWduaW5na2V5Zm9yYXVkaXRsb2dz
ALERT__WEBHOOKURL=
ALERT__NOTIFICATIONTIMEOUTSECONDS=60
ALERT__MAXRETRYATTEMPTS=2
ALERT__RETRYDELAYMS=5000
REDIS__CONNECTIONSTRING=
LOG_LEVEL=Information
API_PORT=5000
OPENTELEMETRY__OTLPENDPOINT=
EOF
cat > docker-compose.yml << EOFCOMPOSE
version: '3.8'
services:
  thinkonerp-api:
    image: ${DOCKER_USER}/thinkonerp-api:v1.0
    container_name: thinkonerp-api
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__OracleDb=\${ORACLE_CONNECTION_STRING}
      - JwtSettings__SecretKey=\${JWT_SECRET_KEY}
      - JwtSettings__Issuer=\${JWT_ISSUER}
      - JwtSettings__Audience=\${JWT_AUDIENCE}
      - JwtSettings__ExpiryInMinutes=\${JWT_EXPIRY_MINUTES}
      - AuditTrail__PayloadLoggingLevel=\${AUDIT__PAYLOADLOGGINGLEVEL}
      - AuditTrail__EncryptionKey=\${AUDIT__ENCRYPTIONKEY}
      - AuditTrail__SigningKey=\${AUDIT__SIGNINGKEY}
      - AlertSettings__WebhookUrl=\${ALERT__WEBHOOKURL}
      - AlertSettings__NotificationTimeoutSeconds=\${ALERT__NOTIFICATIONTIMEOUTSECONDS}
      - AlertSettings__MaxRetryAttempts=\${ALERT__MAXRETRYATTEMPTS}
      - AlertSettings__RetryDelayMs=\${ALERT__RETRYDELAYMS}
      - RedisSettings__ConnectionString=\${REDIS__CONNECTIONSTRING}
      - Serilog__MinimumLevel__Default=\${LOG_LEVEL}
      - OpenTelemetry__OtlpEndpoint=\${OPENTELEMETRY__OTLPENDPOINT}
    ports:
      - "5000:8080"
    restart: unless-stopped
    network_mode: host
    volumes:
      - ./logs:/app/logs
EOFCOMPOSE
docker-compose pull && \
docker-compose up -d && \
sleep 5 && \
echo "" && \
echo "========================================" && \
echo "Deployment Complete!" && \
echo "========================================" && \
echo "" && \
docker ps | grep thinkonerp && \
echo "" && \
echo "Logs:" && \
docker logs thinkonerp-api --tail 30 && \
echo "" && \
echo "Access Swagger: http://178.104.126.99:5000/swagger" && \
echo "Health Check: curl http://localhost:8080/health"
```

**Example:** If your Docker Hub username is `osamashaikh`, change the first line to:
```bash
DOCKER_USER="osamashaikh" && \
```

---

## Option 2: Step-by-Step Commands

If you prefer to run commands one by one:

### Step 1: Set your Docker Hub username
```bash
export DOCKER_USER="YOUR_DOCKERHUB_USERNAME"
```

### Step 2: Navigate to project directory
```bash
cd ~/ThinkOnErp
```

### Step 3: Clean up old deployment
```bash
docker-compose down 2>/dev/null || true
docker stop thinkonerp-api 2>/dev/null || true
docker rm thinkonerp-api 2>/dev/null || true
```

### Step 4: Create .env file
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
ALERT__WEBHOOKURL=
ALERT__NOTIFICATIONTIMEOUTSECONDS=60
ALERT__MAXRETRYATTEMPTS=2
ALERT__RETRYDELAYMS=5000
REDIS__CONNECTIONSTRING=
LOG_LEVEL=Information
API_PORT=5000
OPENTELEMETRY__OTLPENDPOINT=
EOF
```

### Step 5: Create docker-compose.yml
```bash
cat > docker-compose.yml << EOFCOMPOSE
version: '3.8'
services:
  thinkonerp-api:
    image: ${DOCKER_USER}/thinkonerp-api:v1.0
    container_name: thinkonerp-api
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__OracleDb=\${ORACLE_CONNECTION_STRING}
      - JwtSettings__SecretKey=\${JWT_SECRET_KEY}
      - JwtSettings__Issuer=\${JWT_ISSUER}
      - JwtSettings__Audience=\${JWT_AUDIENCE}
      - JwtSettings__ExpiryInMinutes=\${JWT_EXPIRY_MINUTES}
      - AuditTrail__PayloadLoggingLevel=\${AUDIT__PAYLOADLOGGINGLEVEL}
      - AuditTrail__EncryptionKey=\${AUDIT__ENCRYPTIONKEY}
      - AuditTrail__SigningKey=\${AUDIT__SIGNINGKEY}
      - AlertSettings__WebhookUrl=\${ALERT__WEBHOOKURL}
      - AlertSettings__NotificationTimeoutSeconds=\${ALERT__NOTIFICATIONTIMEOUTSECONDS}
      - AlertSettings__MaxRetryAttempts=\${ALERT__MAXRETRYATTEMPTS}
      - AlertSettings__RetryDelayMs=\${ALERT__RETRYDELAYMS}
      - RedisSettings__ConnectionString=\${REDIS__CONNECTIONSTRING}
      - Serilog__MinimumLevel__Default=\${LOG_LEVEL}
      - OpenTelemetry__OtlpEndpoint=\${OPENTELEMETRY__OTLPENDPOINT}
    ports:
      - "5000:8080"
    restart: unless-stopped
    network_mode: host
    volumes:
      - ./logs:/app/logs
EOFCOMPOSE
```

### Step 6: Pull image and start
```bash
docker-compose pull
docker-compose up -d
```

### Step 7: Check logs
```bash
docker logs -f thinkonerp-api
```

Press `Ctrl+C` to stop watching logs.

---

## Verification Commands

### Check container status
```bash
docker ps
```

### Test health endpoint
```bash
curl http://localhost:8080/health
```

### Test login API
```bash
curl -X POST http://178.104.126.99:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"superadmin","password":"Admin@123"}'
```

### View environment variables in container
```bash
docker exec thinkonerp-api env | grep -E "JWT|ORACLE|AUDIT"
```

---

## Troubleshooting Commands

### View last 50 lines of logs
```bash
docker logs thinkonerp-api --tail 50
```

### Restart container
```bash
docker-compose restart
```

### Stop and remove container
```bash
docker-compose down
```

### Check if port 5000 is in use
```bash
netstat -tulpn | grep 5000
```

### Remove old images
```bash
docker image prune -a
```

---

## Update to New Version

When you push a new version (e.g., v1.1) to Docker Hub:

```bash
cd ~/ThinkOnErp
sed -i 's/:v1.0/:v1.1/g' docker-compose.yml
docker-compose pull
docker-compose up -d
docker logs -f thinkonerp-api
```

---

## Access Points

| Service | URL |
|---------|-----|
| **Swagger UI** | http://178.104.126.99:5000/swagger |
| **Health Check** | http://178.104.126.99:5000/health |
| **API Base** | http://178.104.126.99:5000 |
| **Login Endpoint** | http://178.104.126.99:5000/api/auth/login |

---

## Test Credentials

| Username | Password | Role |
|----------|----------|------|
| superadmin | Admin@123 | Super Admin |
| moe | Admin@123 | Admin |
| user1 | User@123 | User |

