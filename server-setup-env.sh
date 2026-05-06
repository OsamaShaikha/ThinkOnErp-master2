#!/bin/bash
# Server-side script to create .env file after git pull
# Run this on the server: bash server-setup-env.sh

set -e

echo "=========================================="
echo "ThinkOnErp - Server Environment Setup"
echo "=========================================="
echo ""

# Check if we're in the right directory
if [ ! -f "ThinkOnErp.sln" ]; then
    echo "❌ Error: Not in ThinkOnErp directory!"
    echo "Please run: cd ~/ThinkOnErp && bash server-setup-env.sh"
    exit 1
fi

echo "✅ In correct directory: $(pwd)"
echo ""

# Create .env file
echo "📝 Creating .env file..."
cat > .env << 'EOF'
# ThinkOnErp Production Environment Configuration
# Server: 178.104.126.99

# =============================================================================
# DATABASE CONNECTION
# =============================================================================
ORACLE_CONNECTION_STRING=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=178.104.126.99:1521/XEPDB1

# =============================================================================
# JWT AUTHENTICATION SETTINGS
# =============================================================================
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGenerationAndValidation123!
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60

# =============================================================================
# AUDIT TRAIL SETTINGS
# =============================================================================
AUDIT__PAYLOADLOGGINGLEVEL=MetadataOnly
AUDIT__ENCRYPTIONKEY=dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz
AUDIT__SIGNINGKEY=dGhpc2lzYXRlc3RzaWduaW5na2V5Zm9yYXVkaXRsb2dz

# =============================================================================
# ALERT & NOTIFICATION SETTINGS
# =============================================================================
ALERT__WEBHOOKURL=https://example.com/webhook
ALERT__NOTIFICATIONTIMEOUTSECONDS=30
ALERT__MAXRETRYATTEMPTS=2
ALERT__RETRYDELAYMS=5000

# =============================================================================
# REDIS CACHE (OPTIONAL)
# =============================================================================
REDIS__CONNECTIONSTRING=

# =============================================================================
# LOGGING
# =============================================================================
LOG_LEVEL=Information

# =============================================================================
# API SETTINGS
# =============================================================================
API_PORT=5000

# =============================================================================
# OPENTELEMETRY (OPTIONAL)
# =============================================================================
OPENTELEMETRY__OTLPENDPOINT=
EOF

if [ $? -eq 0 ]; then
    echo "✅ .env file created successfully"
else
    echo "❌ Failed to create .env file"
    exit 1
fi

echo ""
echo "📋 .env file contents:"
head -20 .env
echo "..."
echo ""

# Restart Docker container
echo "🔄 Restarting Docker container..."
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d

echo ""
echo "⏳ Waiting for container to start (10 seconds)..."
sleep 10

echo ""
echo "📊 Container logs (last 30 lines):"
docker logs --tail 30 thinkonerp-api

echo ""
echo "🔍 Container status:"
docker ps | grep thinkonerp-api || echo "⚠️  Container not running!"

echo ""
echo "=========================================="
echo "✅ Setup Complete!"
echo "=========================================="
echo ""
echo "Test your API:"
echo "  curl http://localhost:8080/health"
echo "  curl http://178.104.126.99:5000/health"
echo ""
echo "Access Swagger:"
echo "  http://178.104.126.99:5000/swagger"
echo ""
