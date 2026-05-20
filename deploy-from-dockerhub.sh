#!/bin/bash
# ThinkOnErp - Deploy from Docker Hub
# Assumes Oracle DB is running natively on the server (not in Docker)

set -e

echo "=========================================="
echo "ThinkOnErp Docker Hub Deployment"
echo "=========================================="
echo ""

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "Docker is not installed. Please install Docker first."
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo "Docker Compose is not installed. Please install Docker first."
    exit 1
fi

# Check if curl is available (needed for healthcheck)
if ! command -v curl &> /dev/null; then
    echo "Installing curl..."
    apt-get update && apt-get install -y curl
fi

# Create project directory
echo "Creating project directory..."
mkdir -p ~/ThinkOnErp
cd ~/ThinkOnErp

# Ask for Oracle connection details
echo ""
echo "=========================================="
echo "Oracle Database Connection"
echo "=========================================="
read -p "Oracle Host [178.104.126.99]: " ORACLE_HOST
ORACLE_HOST=${ORACLE_HOST:-178.104.126.99}

read -p "Oracle Port [1539]: " ORACLE_PORT
ORACLE_PORT=${ORACLE_PORT:-1539}

read -p "Oracle Service Name [free]: " ORACLE_SERVICE
ORACLE_SERVICE=${ORACLE_SERVICE:-free}

read -p "Oracle Username [THINKON_ERP]: " ORACLE_USER
ORACLE_USER=${ORACLE_USER:-THINKON_ERP}

read -sp "Oracle Password: " ORACLE_PASSWORD
echo ""
ORACLE_PASSWORD=${ORACLE_PASSWORD:-thinkon_erp}

# Build EZConnect connection string
ORACLE_CONNECTION_STRING="User Id=${ORACLE_USER};Password=${ORACLE_PASSWORD};Data Source=${ORACLE_HOST}:${ORACLE_PORT}/${ORACLE_SERVICE}"

echo ""
echo "Using connection: ${ORACLE_USER}@${ORACLE_HOST}:${ORACLE_PORT}/${ORACLE_SERVICE}"

# Ask for JWT secret
echo ""
echo "=========================================="
echo "JWT Settings"
echo "=========================================="
read -sp "JWT Secret Key (press Enter for random): " JWT_INPUT
echo ""
if [ -z "$JWT_INPUT" ]; then
    JWT_SECRET_KEY=$(openssl rand -base64 32 2>/dev/null || echo "ChangeMeToARandomSecretKeyForJWT!")
else
    JWT_SECRET_KEY="$JWT_INPUT"
fi

# Create .env file
echo "Creating .env file..."
cat > .env << EOF
# Oracle Database Connection
ORACLE_CONNECTION_STRING=${ORACLE_CONNECTION_STRING}

# JWT Settings
JWT_SECRET_KEY=${JWT_SECRET_KEY}
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60

# Logging
LOG_LEVEL=Information
EOF

echo ".env file created"

# Create docker-compose.yml
echo "Creating docker-compose.yml..."
cat > docker-compose.yml << 'EOF'
version: '3.8'

services:
  thinkonerp-api:
    image: devosamaarori/thinkonerp-api:latest
    container_name: thinkonerp-api
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__OracleDb=${ORACLE_CONNECTION_STRING}
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - JwtSettings__Issuer=${JWT_ISSUER:-ThinkOnErpAPI}
      - JwtSettings__Audience=${JWT_AUDIENCE:-ThinkOnErpClient}
      - JwtSettings__ExpiryInMinutes=${JWT_EXPIRY_MINUTES:-60}
      - Serilog__MinimumLevel__Default=${LOG_LEVEL:-Information}
    ports:
      - "5000:5000"
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
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

echo "docker-compose.yml created"

# Pull images
echo ""
echo "Pulling image from Docker Hub..."
docker-compose pull

# Start services
echo ""
echo "Starting service..."
docker-compose up -d

echo ""
echo "=========================================="
echo "Deployment Complete!"
echo "=========================================="
echo ""
echo "Container Status:"
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""
echo "To watch logs:"
echo "  docker logs -f thinkonerp-api"
echo ""
echo "Access Points:"
echo "  API: http://$(hostname -I | awk '{print $1}'):5000"
echo "  Health: http://$(hostname -I | awk '{print $1}'):5000/health"
echo "  Swagger: http://$(hostname -I | awk '{print $1}'):5000/swagger"
echo ""
echo "Test Credentials:"
echo "  Username: superadmin"
echo "  Password: Admin@123"
echo ""
echo "=========================================="
