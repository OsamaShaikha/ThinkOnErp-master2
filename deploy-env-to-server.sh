#!/bin/bash

# Deploy .env file to server and restart Docker container
# Usage: ./deploy-env-to-server.sh

set -e

SERVER_USER="root"
SERVER_HOST="178.104.126.99"
SERVER_PATH="~/ThinkOnErp"

echo "=========================================="
echo "ThinkOnErp - Deploy Environment Config"
echo "=========================================="
echo ""

# Check if .env.production exists
if [ ! -f ".env.production" ]; then
    echo "❌ Error: .env.production file not found!"
    echo "Please ensure .env.production exists in the current directory."
    exit 1
fi

echo "📋 Configuration file found: .env.production"
echo ""

# Display configuration summary (without sensitive values)
echo "Configuration Summary:"
echo "  - Database: Oracle @ 178.104.126.99:1521/XEPDB1"
echo "  - JWT: Configured"
echo "  - Audit Trail: MetadataOnly logging"
echo "  - Alerts: Configured with 2 retry attempts"
echo "  - Redis: Disabled (optional)"
echo ""

# Confirm deployment
read -p "Deploy this configuration to ${SERVER_HOST}? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "❌ Deployment cancelled."
    exit 1
fi

echo ""
echo "🚀 Deploying configuration to server..."
echo ""

# Copy .env.production to server as .env
echo "📤 Uploading .env file to server..."
scp .env.production ${SERVER_USER}@${SERVER_HOST}:${SERVER_PATH}/.env

if [ $? -ne 0 ]; then
    echo "❌ Failed to upload .env file to server!"
    exit 1
fi

echo "✅ Configuration file uploaded successfully"
echo ""

# Restart Docker container on server
echo "🔄 Restarting Docker container on server..."
ssh ${SERVER_USER}@${SERVER_HOST} << 'ENDSSH'
cd ~/ThinkOnErp

echo "Stopping container..."
docker-compose -f docker-compose.simple.yml down

echo "Starting container with new configuration..."
docker-compose -f docker-compose.simple.yml up -d

echo ""
echo "Waiting for container to start (10 seconds)..."
sleep 10

echo ""
echo "📊 Container logs (last 30 lines):"
docker logs --tail 30 thinkonerp-api

echo ""
echo "🔍 Container status:"
docker ps | grep thinkonerp-api || echo "⚠️  Container not running!"
ENDSSH

if [ $? -ne 0 ]; then
    echo ""
    echo "❌ Failed to restart container on server!"
    exit 1
fi

echo ""
echo "=========================================="
echo "✅ Deployment Complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "  1. Check logs: ssh root@178.104.126.99 'docker logs -f thinkonerp-api'"
echo "  2. Test API: curl http://178.104.126.99:5000/health"
echo "  3. Access Swagger: http://178.104.126.99:5000/swagger"
echo ""
