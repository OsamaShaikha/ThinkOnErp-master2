#!/bin/bash
# ThinkOnErp - Deploy from Docker Hub
# This script sets up everything on your cloud server

set -e

echo "=========================================="
echo "ThinkOnErp Docker Hub Deployment"
echo "=========================================="
echo ""

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Prompt for Docker Hub username
read -p "Enter your Docker Hub username: " DOCKER_USERNAME

if [ -z "$DOCKER_USERNAME" ]; then
    echo "❌ Docker Hub username is required"
    exit 1
fi

# Create project directory
echo "📁 Creating project directory..."
mkdir -p ~/ThinkOnErp
cd ~/ThinkOnErp

# Create .env file
echo "📝 Creating .env file..."
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

echo "✅ .env file created"

# Create docker-compose.yml
echo "📝 Creating docker-compose.yml..."
cat > docker-compose.yml << EOF
version: '3.8'

services:
  # Oracle Database with ThinkOnErp Schema Pre-loaded
  oracle-db:
    image: ${DOCKER_USERNAME}/thinkonerp-oracle:v1.0
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
    image: ${DOCKER_USERNAME}/thinkonerp-api:v1.0
    container_name: thinkonerp-api
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__OracleDb=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=oracle-db:1521/XEPDB1
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
      - "\${API_PORT:-5000}:8080"
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

echo "✅ docker-compose.yml created"

# Pull images
echo ""
echo "📥 Pulling images from Docker Hub..."
echo "This may take 5-10 minutes depending on your internet speed..."
docker-compose pull

# Start services
echo ""
echo "🚀 Starting services..."
docker-compose up -d

echo ""
echo "=========================================="
echo "✅ Deployment Complete!"
echo "=========================================="
echo ""
echo "📊 Container Status:"
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""
echo "⏳ Oracle Database is initializing..."
echo "   This takes 2-3 minutes on first start."
echo ""
echo "📝 To watch logs:"
echo "   docker logs -f thinkonerp-oracle"
echo "   docker logs -f thinkonerp-api"
echo ""
echo "🌐 Access Points:"
echo "   Swagger UI: http://$(hostname -I | awk '{print $1}'):5000/swagger"
echo "   Health Check: http://$(hostname -I | awk '{print $1}'):5000/health"
echo ""
echo "🔐 Test Credentials:"
echo "   Username: superadmin"
echo "   Password: Admin@123"
echo ""
echo "=========================================="
