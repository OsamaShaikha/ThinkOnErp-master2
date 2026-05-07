#!/bin/bash
# Emergency fix for corrupted .env file
# Run on server: cd ~/ThinkOnErp && bash fix-env-file.sh

set -e

echo "=========================================="
echo "FIXING CORRUPTED .env FILE"
echo "=========================================="

cd ~/ThinkOnErp

# Delete corrupted file
rm -f .env

# Create new file with proper line breaks
cat > .env << 'ENVFILE'
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
ENVFILE

echo ""
echo "✅ New .env file created"
echo ""
echo "📋 Verifying file contents (first 10 lines):"
head -10 .env
echo ""
echo "🔍 Checking for corruption:"
if grep -q "AUDIT__SIGNINGKEY" .env; then
    echo "❌ ERROR: File still corrupted (found AUDIT__SIGNINGKEY typo)"
    exit 1
elif grep -q "ALERT__WEBHOOKURLALERT" .env; then
    echo "❌ ERROR: File still corrupted (found merged ALERT variables)"
    exit 1
else
    echo "✅ File looks good - no corruption detected"
fi

echo ""
echo "🔄 Restarting Docker container..."
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d

echo ""
echo "⏳ Waiting 10 seconds for container to start..."
sleep 10

echo ""
echo "📊 Container logs:"
docker logs --tail 50 thinkonerp-api

echo ""
echo "🔍 Checking for success..."
if docker logs thinkonerp-api 2>&1 | grep -q "Now listening on"; then
    echo ""
    echo "=========================================="
    echo "✅ SUCCESS! API IS RUNNING!"
    echo "=========================================="
    echo ""
    echo "Test your API:"
    echo "  curl http://localhost:8080/health"
    echo "  curl http://178.104.126.99:5000/swagger"
    echo ""
else
    echo ""
    echo "⚠️  Container may still be starting or there's an error"
    echo "Check logs with: docker logs thinkonerp-api"
    echo ""
fi
